using BookingPlatform.Domain.Appointments;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace BookingPlatform.Api.Services;

public sealed class AppointmentSchedulingService : IAppointmentSchedulingService
{
    private readonly BookingDbContext _dbContext;
    private readonly TimeZoneInfo _businessTimeZone;

    public AppointmentSchedulingService(BookingDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;

        var timeZoneId = configuration["Scheduling:TimeZoneId"];

        if (string.IsNullOrWhiteSpace(timeZoneId))
            timeZoneId = "Europe/Belgrade";

        _businessTimeZone = ResolveBusinessTimeZone(timeZoneId);
    }

    public async Task<int> GetTotalServiceDurationAsync(
        long serviceId,
        int fallbackDurationMin,
        CancellationToken cancellationToken)
    {
        var stepDurations = await _dbContext.ServiceSteps
            .AsNoTracking()
            .Where(x => x.ServiceId == serviceId)
            .Select(x => x.DurationMin)
            .ToListAsync(cancellationToken);

        if (stepDurations.Count == 0)
            return fallbackDurationMin;

        return stepDurations.Sum();
    }

    public async Task<bool> IsStartAlignedToBusinessSlotGridAsync(
        long businessId,
        long staffMemberId,
        DateTime startAtUtc,
        CancellationToken cancellationToken)
    {
        var business = await _dbContext.Businesses
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == businessId && x.IsActive,
                cancellationToken);

        if (business is null)
            return false;

        var normalizedStartUtc = startAtUtc.Kind == DateTimeKind.Utc
            ? startAtUtc
            : DateTime.SpecifyKind(startAtUtc, DateTimeKind.Utc);

        var localStart = UtcToLocal(normalizedStartUtc);
        var targetLocalDate = localStart.Date;

        var dayOfWeek = targetLocalDate.DayOfWeek switch
        {
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday => 4,
            DayOfWeek.Friday => 5,
            DayOfWeek.Saturday => 6,
            _ => 7
        };

        var businessHours = await _dbContext.BusinessWorkingHours
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.BusinessId == businessId && x.DayOfWeek == dayOfWeek,
                cancellationToken);

        if (businessHours is null || businessHours.IsClosed)
            return false;

        var staffWorkingRanges = await GetEffectiveStaffWorkingRangesAsync(
            staffMemberId,
            targetLocalDate,
            dayOfWeek,
            cancellationToken);

        if (staffWorkingRanges.Count == 0)
            return false;

        var effectiveUtcRanges = staffWorkingRanges
            .Select(x => new
            {
                StartTime = businessHours.StartTime > x.StartTime
                    ? businessHours.StartTime
                    : x.StartTime,

                EndTime = businessHours.EndTime < x.EndTime
                    ? businessHours.EndTime
                    : x.EndTime
            })
            .Where(x => x.EndTime > x.StartTime)
            .Select(x => new
            {
                StartUtc = LocalDateTimeToUtc(targetLocalDate, x.StartTime),
                EndUtc = LocalDateTimeToUtc(targetLocalDate, x.EndTime)
            })
            .Where(x => x.EndUtc > x.StartUtc)
            .ToList();

        if (effectiveUtcRanges.Count == 0)
            return false;

        var matchingRange = effectiveUtcRanges.FirstOrDefault(x =>
            normalizedStartUtc >= x.StartUtc &&
            normalizedStartUtc < x.EndUtc);

        if (matchingRange is null)
            return false;

        var dayStartUtc = matchingRange.StartUtc;
        var dayEndUtc = matchingRange.EndUtc;

        Console.WriteLine("========== SLOT GRID SERVICE DEBUG ==========");
        Console.WriteLine($"TimeZone={_businessTimeZone.Id}");
        Console.WriteLine($"StartAtUtc={normalizedStartUtc:O} Kind={normalizedStartUtc.Kind}");
        Console.WriteLine($"LocalStart={localStart:O} Kind={localStart.Kind}");
        Console.WriteLine($"TargetLocalDate={targetLocalDate:yyyy-MM-dd}");
        Console.WriteLine($"DayStartUtc={dayStartUtc:O}");
        Console.WriteLine($"DayEndUtc={dayEndUtc:O}");
        Console.WriteLine("=============================================");

        if (normalizedStartUtc < dayStartUtc || normalizedStartUtc >= dayEndUtc)
            return false;

        var slotStepMinutes = business.SlotIntervalMin > 0
            ? business.SlotIntervalMin
            : 30;

        var totalMinutesFromStart = (normalizedStartUtc - dayStartUtc).TotalMinutes;

        if (totalMinutesFromStart < 0)
            return false;

        return totalMinutesFromStart % slotStepMinutes == 0;
    }

    public async Task<AppointmentAvailabilityResult> CheckAvailabilityAsync(
    long businessId,
    long serviceId,
    long staffMemberId,
    long? resourceId,
    DateTime startAtUtc,
    DateTime endAtUtc,
    long? ignoreAppointmentId,
    bool ignoreWorkingHours,
    bool ignoreTimeOffBlocks,
    bool ignoreAppointmentConflicts,
    CancellationToken cancellationToken)
    {
        var result = new AppointmentAvailabilityResult
        {
            IsAvailable = true,
            Message = "Izabrani termin je dostupan.",
            ReasonCode = "available"
        };

        var normalizedStartUtc = startAtUtc.Kind == DateTimeKind.Utc
            ? startAtUtc
            : DateTime.SpecifyKind(startAtUtc, DateTimeKind.Utc);

        var normalizedEndUtc = endAtUtc.Kind == DateTimeKind.Utc
            ? endAtUtc
            : DateTime.SpecifyKind(endAtUtc, DateTimeKind.Utc);

        var localStart = UtcToLocal(normalizedStartUtc);
        var localEnd = UtcToLocal(normalizedEndUtc);

        var targetDate = localStart.Date;

        if (localEnd.Date != targetDate)
        {
            result.IsAvailable = false;
            result.Message = "Termin mora biti u okviru istog dana.";
            result.ReasonCode = "cross_day_not_supported";
            return result;
        }

        var dayOfWeek = targetDate.DayOfWeek switch
        {
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday => 4,
            DayOfWeek.Friday => 5,
            DayOfWeek.Saturday => 6,
            _ => 7
        };

        if (!ignoreWorkingHours)
        {
            var businessHours = await _dbContext.BusinessWorkingHours
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.BusinessId == businessId && x.DayOfWeek == dayOfWeek,
                    cancellationToken);

            if (businessHours is null || businessHours.IsClosed)
                result.HasBusinessHoursViolation = true;

            var staffWorkingRanges = await GetEffectiveStaffWorkingRangesAsync(
                staffMemberId,
                targetDate,
                dayOfWeek,
                cancellationToken);

            if (staffWorkingRanges.Count == 0)
                result.HasStaffHoursViolation = true;

            if (!result.HasBusinessHoursViolation && !result.HasStaffHoursViolation)
            {
                var effectiveUtcRanges = staffWorkingRanges
                    .Select(x => new
                    {
                        StartTime = businessHours!.StartTime > x.StartTime
                            ? businessHours.StartTime
                            : x.StartTime,

                        EndTime = businessHours.EndTime < x.EndTime
                            ? businessHours.EndTime
                            : x.EndTime
                    })
                    .Where(x => x.EndTime > x.StartTime)
                    .Select(x => new
                    {
                        StartUtc = LocalDateTimeToUtc(targetDate, x.StartTime),
                        EndUtc = LocalDateTimeToUtc(targetDate, x.EndTime)
                    })
                    .Where(x => x.EndUtc > x.StartUtc)
                    .ToList();

                var fitsInOneWorkingRange = effectiveUtcRanges.Any(x =>
                    normalizedStartUtc >= x.StartUtc &&
                    normalizedEndUtc <= x.EndUtc);

                Console.WriteLine("========== AVAILABILITY SERVICE DEBUG ==========");
                Console.WriteLine($"TimeZone={_businessTimeZone.Id}");
                Console.WriteLine($"StartAtUtc={normalizedStartUtc:O}");
                Console.WriteLine($"EndAtUtc={normalizedEndUtc:O}");
                Console.WriteLine($"LocalStart={localStart:O}");
                Console.WriteLine($"LocalEnd={localEnd:O}");
                Console.WriteLine($"TargetLocalDate={targetDate:yyyy-MM-dd}");

                foreach (var range in effectiveUtcRanges)
                {
                    Console.WriteLine(
                        $"Effective range local={UtcToLocal(range.StartUtc):HH:mm:ss} - {UtcToLocal(range.EndUtc):HH:mm:ss}");
                }

                Console.WriteLine($"FitsInOneWorkingRange={fitsInOneWorkingRange}");
                Console.WriteLine("===============================================");

                if (!fitsInOneWorkingRange)
                {
                    result.HasStaffHoursViolation = true;
                }
            }
        }

        if (!ignoreAppointmentConflicts)
        {
            result.HasAppointmentConflict = await HasServiceStaffTimelineConflictAsync(
                businessId,
                serviceId,
                staffMemberId,
                normalizedStartUtc,
                normalizedEndUtc,
                ignoreAppointmentId,
                cancellationToken);
        }

        if (resourceId.HasValue)
        {
            var resourceCreatesOccupancy = await _dbContext.Resources
                .AsNoTracking()
                .Where(x => x.Id == resourceId.Value && x.BusinessId == businessId)
                .Select(x => x.CreatesOccupancy)
                .FirstOrDefaultAsync(cancellationToken);

            if (resourceCreatesOccupancy)
            {
                result.HasResourceConflict = await _dbContext.Appointments
                    .AsNoTracking()
                    .Join(
                        _dbContext.Resources.AsNoTracking(),
                        appointment => appointment.ResourceId,
                        resource => resource.Id,
                        (appointment, resource) => new { Appointment = appointment, Resource = resource })
                    .AnyAsync(x =>
                        x.Appointment.BusinessId == businessId &&
                        x.Appointment.ResourceId == resourceId.Value &&
                        x.Appointment.Id != ignoreAppointmentId &&
                        x.Appointment.Status != AppointmentStatus.Cancelled &&
                        x.Appointment.Status != AppointmentStatus.Rejected &&
                        x.Resource.CreatesOccupancy &&
normalizedStartUtc < x.Appointment.EndAtUtc &&
normalizedEndUtc > x.Appointment.StartAtUtc,
                        cancellationToken);
            }
            else
            {
                result.HasResourceConflict = false;
            }
        }

        if (!result.HasResourceConflict)
        {
            result.HasResourceConflict = await HasServiceResourceTimelineConflictAsync(
                businessId,
                serviceId,
                staffMemberId,
                normalizedStartUtc,
                ignoreAppointmentId,
                cancellationToken);
        }


        if (!ignoreTimeOffBlocks)
        {
            result.HasTimeOffConflict = await _dbContext.TimeOffBlocks
                .AsNoTracking()
                .AnyAsync(x =>
                    x.BusinessId == businessId &&
                    (x.StaffMemberId == null || x.StaffMemberId == staffMemberId) &&
normalizedStartUtc < x.EndAtUtc &&
normalizedEndUtc > x.StartAtUtc,
                    cancellationToken);
        }

        result.IsAvailable =
            !result.HasBusinessHoursViolation &&
            !result.HasStaffHoursViolation &&
            !result.HasAppointmentConflict &&
            !result.HasTimeOffConflict &&
            !result.HasResourceConflict;

        if (!result.IsAvailable)
        {
            result.Message = BuildUnavailableMessage(result);
            result.ReasonCode = BuildReasonCode(result);
        }

        return result;
    }

    private async Task<List<(TimeSpan StartTime, TimeSpan EndTime)>> GetEffectiveStaffWorkingRangesAsync(
    long staffMemberId,
    DateTime targetDate,
    int dayOfWeek,
    CancellationToken cancellationToken)
    {
        var staff = await _dbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == staffMemberId, cancellationToken);

        if (staff is null)
            return new List<(TimeSpan StartTime, TimeSpan EndTime)>();

        var scheduleMode = (int)staff.ScheduleMode;

        if (scheduleMode == 0)
        {
            var fixedRuleRange = await GetFixedScheduleRuleRangeAsync(
                staffMemberId,
                dayOfWeek,
                cancellationToken);

            if (fixedRuleRange is not null)
                return new List<(TimeSpan StartTime, TimeSpan EndTime)> { fixedRuleRange.Value };

            var legacyRange = await GetLegacyStaffWorkingHoursRangeAsync(
                staffMemberId,
                dayOfWeek,
                cancellationToken);

            return legacyRange is null
                ? new List<(TimeSpan StartTime, TimeSpan EndTime)>()
                : new List<(TimeSpan StartTime, TimeSpan EndTime)> { legacyRange.Value };
        }

        if (scheduleMode == 1)
        {
            var shiftRange = await GetShiftScheduleRuleRangeAsync(
                staffMemberId,
                targetDate,
                dayOfWeek,
                cancellationToken);

            return shiftRange is null
                ? new List<(TimeSpan StartTime, TimeSpan EndTime)>()
                : new List<(TimeSpan StartTime, TimeSpan EndTime)> { shiftRange.Value };
        }

        if (scheduleMode == 2)
        {
            return await GetSplitScheduleRuleRangesAsync(
                staffMemberId,
                dayOfWeek,
                cancellationToken);
        }

        var fallbackRange = await GetLegacyStaffWorkingHoursRangeAsync(
            staffMemberId,
            dayOfWeek,
            cancellationToken);

        return fallbackRange is null
            ? new List<(TimeSpan StartTime, TimeSpan EndTime)>()
            : new List<(TimeSpan StartTime, TimeSpan EndTime)> { fallbackRange.Value };
    }

    private async Task<List<(TimeSpan StartTime, TimeSpan EndTime)>> GetSplitScheduleRuleRangesAsync(
    long staffMemberId,
    int dayOfWeek,
    CancellationToken cancellationToken)
    {
        var rules = await _dbContext.StaffScheduleRules
            .AsNoTracking()
            .Where(x =>
                x.StaffMemberId == staffMemberId &&
                x.DayOfWeek == dayOfWeek &&
                x.IsActive &&
                ((int)x.SegmentType == 3 || (int)x.SegmentType == 4))
            .OrderBy(x => x.StartTime)
            .ToListAsync(cancellationToken);

        return rules
            .Where(x => x.EndTime > x.StartTime)
            .Select(x => (x.StartTime, x.EndTime))
            .ToList();
    }


    private async Task<(TimeSpan StartTime, TimeSpan EndTime)?> GetLegacyStaffWorkingHoursRangeAsync(
        long staffMemberId,
        int dayOfWeek,
        CancellationToken cancellationToken)
    {
        var staffHours = await _dbContext.StaffWorkingHours
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.StaffMemberId == staffMemberId && x.DayOfWeek == dayOfWeek,
                cancellationToken);

        if (staffHours is null || staffHours.IsClosed)
            return null;

        if (staffHours.EndTime <= staffHours.StartTime)
            return null;

        return (staffHours.StartTime, staffHours.EndTime);
    }

    private async Task<(TimeSpan StartTime, TimeSpan EndTime)?> GetFixedScheduleRuleRangeAsync(
        long staffMemberId,
        int dayOfWeek,
        CancellationToken cancellationToken)
    {
        var rule = await _dbContext.StaffScheduleRules
            .AsNoTracking()
            .Where(x =>
                x.StaffMemberId == staffMemberId &&
                x.DayOfWeek == dayOfWeek &&
                x.IsActive &&
                (int)x.WeekType == 0 &&
                (int)x.SegmentType == 0)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (rule is null)
            return null;

        if (rule.EndTime <= rule.StartTime)
            return null;

        return (rule.StartTime, rule.EndTime);
    }

    private async Task<(TimeSpan StartTime, TimeSpan EndTime)?> GetShiftScheduleRuleRangeAsync(
        long staffMemberId,
        DateTime targetDate,
        int dayOfWeek,
        CancellationToken cancellationToken)
    {
        var overrideDate = DateTime.SpecifyKind(targetDate.Date, DateTimeKind.Utc);

        var overrideItem = await _dbContext.StaffScheduleOverrides
            .AsNoTracking()
            .Where(x =>
                x.StaffMemberId == staffMemberId &&
                x.Date == overrideDate)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (overrideItem is not null)
        {
            if (overrideItem.IsDayOff)
                return null;

            if (overrideItem.StartTime.HasValue &&
                overrideItem.EndTime.HasValue &&
                overrideItem.EndTime.Value > overrideItem.StartTime.Value)
            {
                return (overrideItem.StartTime.Value, overrideItem.EndTime.Value);
            }
        }

        var dayRules = await _dbContext.StaffScheduleRules
            .AsNoTracking()
            .Where(x =>
                x.StaffMemberId == staffMemberId &&
                x.DayOfWeek == dayOfWeek &&
                x.IsActive)
            .ToListAsync(cancellationToken);

        if (dayRules.Count == 0)
            return null;

        int wantedShift;

        if (overrideItem?.ShiftType is not null &&
            ((int)overrideItem.ShiftType.Value == 1 || (int)overrideItem.ShiftType.Value == 2))
        {
            wantedShift = (int)overrideItem.ShiftType.Value;
        }
        else
        {
            var hasRotatingRules = dayRules.Any(x =>
                (int)x.WeekType == 1 ||
                (int)x.WeekType == 2);

            if (hasRotatingRules)
            {
                var weekOfYear = ISOWeek.GetWeekOfYear(targetDate);
                var wantedWeekType = weekOfYear % 2 == 0 ? 2 : 1;

                var weekRule = dayRules
                    .Where(x =>
                        (int)x.WeekType == wantedWeekType &&
                        ((int)x.SegmentType == 1 || (int)x.SegmentType == 2))
                    .OrderBy(x => x.Id)
                    .FirstOrDefault();

                if (weekRule is null)
                    return null;

                wantedShift = (int)weekRule.SegmentType;
            }
            else
            {
                var fixedShiftRule = dayRules
                    .Where(x =>
                        (int)x.WeekType == 0 &&
                        ((int)x.SegmentType == 1 || (int)x.SegmentType == 2))
                    .OrderBy(x => x.Id)
                    .FirstOrDefault();

                if (fixedShiftRule is null)
                    return null;

                wantedShift = (int)fixedShiftRule.SegmentType;
            }
        }

        var rule = dayRules
            .Where(x =>
                (int)x.SegmentType == wantedShift &&
                ((int)x.WeekType == 0 || (int)x.WeekType == 1 || (int)x.WeekType == 2))
            .OrderBy(x => (int)x.WeekType == 0 ? 0 : 1)
            .ThenBy(x => x.Id)
            .FirstOrDefault();

        if (rule is null)
            return null;

        if (rule.EndTime <= rule.StartTime)
            return null;

        return (rule.StartTime, rule.EndTime);
    }

    private static long GetResourcePoolKey(long resourceId, long? resourceGroupId)
    {
        return resourceGroupId.HasValue
            ? -resourceGroupId.Value
            : resourceId;
    }

    public async Task CreateAppointmentStaffUsagesAsync(
    long appointmentId,
    long businessId,
    long serviceId,
    long primaryStaffMemberId,
    DateTime appointmentStartAtUtc,
    DateTime appointmentEndAtUtc,
    CancellationToken cancellationToken)
    {
        var existing = await _dbContext.AppointmentStaffUsages
            .Where(x => x.AppointmentId == appointmentId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
            _dbContext.AppointmentStaffUsages.RemoveRange(existing);

        var serviceUsages = await _dbContext.ServiceResourceUsages
            .AsNoTracking()
            .Where(x => x.ServiceId == serviceId)
            .OrderBy(x => x.StartMinute)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (serviceUsages.Count == 0)
        {
            var totalMinutes = (int)Math.Max(1, (appointmentEndAtUtc - appointmentStartAtUtc).TotalMinutes);

            _dbContext.AppointmentStaffUsages.Add(new AppointmentStaffUsage
            {
                AppointmentId = appointmentId,
                StaffMemberId = primaryStaffMemberId,
                StartMinute = 0,
                DurationMin = totalMinutes,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var usageIds = serviceUsages
            .Select(x => x.Id)
            .Distinct()
            .ToList();

        var usageStaffLinks = await _dbContext.ServiceResourceUsageStaffMembers
            .AsNoTracking()
            .Where(x => usageIds.Contains(x.ServiceResourceUsageId))
            .Select(x => new
            {
                x.ServiceResourceUsageId,
                x.StaffMemberId
            })
            .ToListAsync(cancellationToken);

        var usageStaffMap = usageStaffLinks
            .GroupBy(x => x.ServiceResourceUsageId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.StaffMemberId).Distinct().OrderBy(id => id).ToList());

        var resourceIds = serviceUsages
            .Select(x => x.ResourceId)
            .Distinct()
            .ToList();

        var resources = await _dbContext.Resources
            .AsNoTracking()
            .Where(x =>
                resourceIds.Contains(x.Id) &&
                x.BusinessId == businessId &&
                x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.CreatesOccupancy,
                x.AllowParallelUsage
            })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var now = DateTime.UtcNow;
        var pendingStaffUsages = new List<(long StaffMemberId, int StartMinute, int DurationMin)>();

        foreach (var usage in serviceUsages)
        {
            if (!resources.TryGetValue(usage.ResourceId, out var resource))
                continue;

            var explicitStaffIds = new List<long>();

            if (usageStaffMap.TryGetValue(usage.Id, out var linkedStaffIds))
                explicitStaffIds.AddRange(linkedStaffIds);

            if (usage.StaffId.HasValue && usage.StaffId.Value > 0)
                explicitStaffIds.Add(usage.StaffId.Value);

            explicitStaffIds = explicitStaffIds
                .Where(x => x > 0)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            List<long> allowedStaffIds;

            if (explicitStaffIds.Count > 0)
            {
                allowedStaffIds = explicitStaffIds;
            }
            else
            {
                // Resurs koji ne pravi zauzeće i nema izabrane radnike tretiramo kao deo bez radnika.
                // Primer: Čekanje.
                if (!resource.CreatesOccupancy || resource.AllowParallelUsage)
                    continue;

                throw new InvalidOperationException("Za jedan deo usluge nije podešen nijedan radnik.");
            }

            allowedStaffIds = allowedStaffIds
                .Where(x => x > 0)
                .Distinct()
                .OrderBy(x => x == primaryStaffMemberId ? 0 : 1)
                .ThenBy(x => x)
                .ToList();

            if (allowedStaffIds.Count == 0)
                throw new InvalidOperationException("Za jedan deo usluge nije podešen nijedan radnik.");

            var segmentStartUtc = appointmentStartAtUtc.AddMinutes(usage.StartMinute);
            var segmentEndUtc = segmentStartUtc.AddMinutes(usage.DurationMin);

            if (segmentStartUtc >= appointmentEndAtUtc)
                continue;

            if (segmentEndUtc > appointmentEndAtUtc)
                segmentEndUtc = appointmentEndAtUtc;

            if (segmentEndUtc <= segmentStartUtc)
                continue;

            long? selectedStaffId;

            // Ako je glavni radnik čekiran za ovaj deo,
            // ovaj deo MORA da radi glavni radnik.
            if (allowedStaffIds.Contains(primaryStaffMemberId))
            {
                var primaryIsFree = await IsStaffFreeForSegmentAsync(
                    businessId,
                    primaryStaffMemberId,
                    segmentStartUtc,
                    segmentEndUtc,
                    appointmentId,
                    cancellationToken);

                if (!primaryIsFree)
                    throw new InvalidOperationException("Glavni radnik nije slobodan za jedan deo usluge.");

                selectedStaffId = primaryStaffMemberId;
            }
            else
            {
                selectedStaffId = null;

                foreach (var staffId in allowedStaffIds)
                {
                    var isFree = await IsStaffFreeForSegmentAsync(
                        businessId,
                        staffId,
                        segmentStartUtc,
                        segmentEndUtc,
                        appointmentId,
                        cancellationToken);

                    if (isFree)
                    {
                        selectedStaffId = staffId;
                        break;
                    }
                }

                if (!selectedStaffId.HasValue)
                    throw new InvalidOperationException("Nije pronađen slobodan radnik za jedan deo usluge.");
            }

            var startMinute = (int)Math.Round((segmentStartUtc - appointmentStartAtUtc).TotalMinutes);
            var durationMin = (int)Math.Round((segmentEndUtc - segmentStartUtc).TotalMinutes);

            if (durationMin <= 0)
                continue;

            pendingStaffUsages.Add((
                selectedStaffId.Value,
                startMinute,
                durationMin));
        }

        if (pendingStaffUsages.Count == 0)
        {
            var totalMinutes = (int)Math.Max(1, (appointmentEndAtUtc - appointmentStartAtUtc).TotalMinutes);

            pendingStaffUsages.Add((
                primaryStaffMemberId,
                0,
                totalMinutes));
        }

        var distinctStaffUsages = pendingStaffUsages
            .Where(x => x.DurationMin > 0)
            .Distinct()
            .OrderBy(x => x.StartMinute)
            .ThenBy(x => x.StaffMemberId)
            .ToList();

        foreach (var item in distinctStaffUsages)
        {
            _dbContext.AppointmentStaffUsages.Add(new AppointmentStaffUsage
            {
                AppointmentId = appointmentId,
                StaffMemberId = item.StaffMemberId,
                StartMinute = item.StartMinute,
                DurationMin = item.DurationMin,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> HasServiceStaffTimelineConflictAsync(
    long businessId,
    long serviceId,
    long primaryStaffMemberId,
    DateTime candidateStartUtc,
    DateTime candidateEndUtc,
    long? ignoreAppointmentId,
    CancellationToken cancellationToken)
    {
        var serviceUsages = await _dbContext.ServiceResourceUsages
            .AsNoTracking()
            .Where(x => x.ServiceId == serviceId)
            .ToListAsync(cancellationToken);

        if (serviceUsages.Count == 0)
        {
            return await HasLegacyPrimaryStaffConflictAsync(
                businessId,
                primaryStaffMemberId,
                candidateStartUtc,
                candidateEndUtc,
                ignoreAppointmentId,
                cancellationToken);
        }

        var usageIds = serviceUsages
            .Select(x => x.Id)
            .Distinct()
            .ToList();

        var usageStaffLinks = await _dbContext.ServiceResourceUsageStaffMembers
            .AsNoTracking()
            .Where(x => usageIds.Contains(x.ServiceResourceUsageId))
            .Select(x => new
            {
                x.ServiceResourceUsageId,
                x.StaffMemberId
            })
            .ToListAsync(cancellationToken);

        var usageStaffMap = usageStaffLinks
            .GroupBy(x => x.ServiceResourceUsageId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.StaffMemberId).Distinct().ToList());

        var resourceIds = serviceUsages
            .Select(x => x.ResourceId)
            .Distinct()
            .ToList();

        var resources = await _dbContext.Resources
            .AsNoTracking()
            .Where(x =>
                resourceIds.Contains(x.Id) &&
                x.BusinessId == businessId &&
                x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.CreatesOccupancy,
                x.AllowParallelUsage
            })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var hasAnyStaffSegment = false;

        foreach (var usage in serviceUsages)
        {
            if (!resources.TryGetValue(usage.ResourceId, out var resource))
                continue;

            // Čekanje, farba, materijal i slični redovi ne zauzimaju radnika
            // ako ne prave stvarno zauzeće i nemaju izabrane radnike.
            var explicitStaffIds = new List<long>();

            if (usageStaffMap.TryGetValue(usage.Id, out var linkedStaffIds))
            {
                explicitStaffIds.AddRange(linkedStaffIds);
            }

            if (usage.StaffId.HasValue && usage.StaffId.Value > 0)
            {
                explicitStaffIds.Add(usage.StaffId.Value);
            }

            explicitStaffIds = explicitStaffIds
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            List<long> allowedStaffIds;

            if (explicitStaffIds.Count > 0)
            {
                allowedStaffIds = explicitStaffIds;
            }
            else
            {
                // Resurs koji ne pravi zauzeće i nema izabrane radnike tretiramo kao deo bez radnika.
                // Primer: Čekanje.
                if (!resource.CreatesOccupancy || resource.AllowParallelUsage)
                    continue;

                // Ako resurs pravi zauzeće, a nema čekiranih radnika,
                // podešavanje usluge nije ispravno i termin ne sme biti ponuđen.
                return true;
            }

            allowedStaffIds = allowedStaffIds
                .Where(x => x > 0)
                .Distinct()
                .OrderBy(x => x == primaryStaffMemberId ? 0 : 1)
                .ThenBy(x => x)
                .ToList();

            if (allowedStaffIds.Count == 0)
                return true;

            hasAnyStaffSegment = true;

            var segmentStartUtc = candidateStartUtc.AddMinutes(usage.StartMinute);
            var segmentEndUtc = segmentStartUtc.AddMinutes(usage.DurationMin);

            if (segmentEndUtc <= segmentStartUtc)
                continue;

            // Ako je glavni radnik čekiran za ovaj deo,
            // ovaj deo MORA da radi glavni radnik.
            if (allowedStaffIds.Contains(primaryStaffMemberId))
            {
                var primaryIsFree = await IsStaffFreeForSegmentAsync(
                    businessId,
                    primaryStaffMemberId,
                    segmentStartUtc,
                    segmentEndUtc,
                    ignoreAppointmentId,
                    cancellationToken);

                if (!primaryIsFree)
                    return true;

                continue;
            }

            // Ako glavni radnik nije čekiran za ovaj deo,
            // tražimo nekog drugog čekiranog slobodnog radnika.
            var hasFreeHelperStaff = false;

            foreach (var staffId in allowedStaffIds)
            {
                var isFree = await IsStaffFreeForSegmentAsync(
                    businessId,
                    staffId,
                    segmentStartUtc,
                    segmentEndUtc,
                    ignoreAppointmentId,
                    cancellationToken);

                if (isFree)
                {
                    hasFreeHelperStaff = true;
                    break;
                }
            }

            if (!hasFreeHelperStaff)
                return true;
        }

        if (!hasAnyStaffSegment)
        {
            return await HasLegacyPrimaryStaffConflictAsync(
                businessId,
                primaryStaffMemberId,
                candidateStartUtc,
                candidateEndUtc,
                ignoreAppointmentId,
                cancellationToken);
        }

        return false;
    }

    private async Task<bool> IsStaffFreeForSegmentAsync(
    long businessId,
    long staffMemberId,
    DateTime segmentStartUtc,
    DateTime segmentEndUtc,
    long? ignoreAppointmentId,
    CancellationToken cancellationToken)
    {
        var hasStaffUsageConflict = await _dbContext.AppointmentStaffUsages
            .AsNoTracking()
            .AnyAsync(x =>
                x.StaffMemberId == staffMemberId &&
                x.Appointment.BusinessId == businessId &&
                x.Appointment.Id != ignoreAppointmentId &&
                x.Appointment.Status != AppointmentStatus.Cancelled &&
                x.Appointment.Status != AppointmentStatus.Rejected &&
                segmentStartUtc < x.Appointment.StartAtUtc.AddMinutes(x.StartMinute + x.DurationMin) &&
                segmentEndUtc > x.Appointment.StartAtUtc.AddMinutes(x.StartMinute),
                cancellationToken);

        if (hasStaffUsageConflict)
            return false;

        var hasLegacyConflict = await _dbContext.Appointments
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.PrimaryStaffMemberId == staffMemberId &&
                x.Id != ignoreAppointmentId &&
                x.Status != AppointmentStatus.Cancelled &&
                x.Status != AppointmentStatus.Rejected &&
                segmentStartUtc < x.EndAtUtc &&
                segmentEndUtc > x.StartAtUtc)
            .Where(x =>
                !_dbContext.AppointmentStaffUsages.Any(u => u.AppointmentId == x.Id))
            .AnyAsync(cancellationToken);

        return !hasLegacyConflict;
    }

    private async Task<bool> HasLegacyPrimaryStaffConflictAsync(
        long businessId,
        long staffMemberId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        long? ignoreAppointmentId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Appointments
            .AsNoTracking()
            .AnyAsync(x =>
                x.BusinessId == businessId &&
                x.PrimaryStaffMemberId == staffMemberId &&
                x.Id != ignoreAppointmentId &&
                x.Status != AppointmentStatus.Cancelled &&
                x.Status != AppointmentStatus.Rejected &&
                startAtUtc < x.EndAtUtc &&
                endAtUtc > x.StartAtUtc,
                cancellationToken);
    }

    private async Task<bool> HasServiceResourceTimelineConflictAsync(
        long businessId,
        long serviceId,
        long staffMemberId,
        DateTime candidateStartUtc,
        long? ignoreAppointmentId,
        CancellationToken cancellationToken)
    {
        var serviceUsages = await _dbContext.ServiceResourceUsages
            .AsNoTracking()
            .Where(x => x.ServiceId == serviceId)
            .ToListAsync(cancellationToken);

        if (serviceUsages.Count == 0)
            return false;

        var usageResourceIds = serviceUsages
            .Select(x => x.ResourceId)
            .Distinct()
            .ToList();

        var usageResources = await _dbContext.Resources
            .AsNoTracking()
            .Where(x =>
                usageResourceIds.Contains(x.Id) &&
                x.BusinessId == businessId &&
                x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.ResourceGroupId,
                x.CreatesOccupancy,
                x.AllowParallelUsage
            })
            .ToListAsync(cancellationToken);

        var resourcesById = usageResources.ToDictionary(x => x.Id);

        var candidateSegments = new List<(long PoolKey, DateTime StartUtc, DateTime EndUtc)>();

        foreach (var usage in serviceUsages)
        {
            if (!resourcesById.TryGetValue(usage.ResourceId, out var resource))
                continue;

            if (!resource.CreatesOccupancy)
                continue;

            if (resource.AllowParallelUsage)
                continue;

            var segmentStartUtc = candidateStartUtc.AddMinutes(usage.StartMinute);
            var segmentEndUtc = segmentStartUtc.AddMinutes(usage.DurationMin);

            if (segmentEndUtc <= segmentStartUtc)
                continue;

            var poolKey = GetResourcePoolKey(resource.Id, resource.ResourceGroupId);

            candidateSegments.Add((poolKey, segmentStartUtc, segmentEndUtc));
        }

        if (candidateSegments.Count == 0)
            return false;

        var groupIds = candidateSegments
            .Where(x => x.PoolKey < 0)
            .Select(x => -x.PoolKey)
            .Distinct()
            .ToList();

        var singleResourceIds = candidateSegments
            .Where(x => x.PoolKey > 0)
            .Select(x => x.PoolKey)
            .Distinct()
            .ToList();

        var poolCapacities = new Dictionary<long, int>();

        if (groupIds.Count > 0)
        {
            var groupCapacities = await _dbContext.Resources
                .AsNoTracking()
                .Where(x =>
                    x.BusinessId == businessId &&
                    x.IsActive &&
                    x.CreatesOccupancy &&
                    !x.AllowParallelUsage &&
                    x.ResourceGroupId.HasValue &&
                    groupIds.Contains(x.ResourceGroupId.Value))
                .GroupBy(x => x.ResourceGroupId!.Value)
                .Select(x => new
                {
                    ResourceGroupId = x.Key,
                    Count = x.Count()
                })
                .ToListAsync(cancellationToken);

            foreach (var item in groupCapacities)
            {
                poolCapacities[-item.ResourceGroupId] = item.Count;
            }
        }

        if (singleResourceIds.Count > 0)
        {
            var existingSingleResources = await _dbContext.Resources
                .AsNoTracking()
                .Where(x =>
                    x.BusinessId == businessId &&
                    x.IsActive &&
                    x.CreatesOccupancy &&
                    !x.AllowParallelUsage &&
                    singleResourceIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            foreach (var singleResourceId in existingSingleResources)
            {
                poolCapacities[singleResourceId] = 1;
            }
        }

        foreach (var segment in candidateSegments)
        {
            if (!poolCapacities.TryGetValue(segment.PoolKey, out var availableCount) ||
                availableCount <= 0)
            {
                return true;
            }
        }

        var candidateWindowStartUtc = candidateSegments.Min(x => x.StartUtc);
        var candidateWindowEndUtc = candidateSegments.Max(x => x.EndUtc);

        var existingAppointmentsQuery = _dbContext.Appointments
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.Status != AppointmentStatus.Cancelled &&
                x.Status != AppointmentStatus.Rejected &&
                x.StartAtUtc < candidateWindowEndUtc &&
                x.EndAtUtc > candidateWindowStartUtc);

        if (ignoreAppointmentId.HasValue)
        {
            existingAppointmentsQuery = existingAppointmentsQuery
                .Where(x => x.Id != ignoreAppointmentId.Value);
        }

        var existingAppointments = await existingAppointmentsQuery
            .ToListAsync(cancellationToken);

        foreach (var candidateSegment in candidateSegments)
        {
            var availableCount = poolCapacities[candidateSegment.PoolKey];

            var ownDemand = candidateSegments.Count(x =>
                x.PoolKey == candidateSegment.PoolKey &&
                candidateSegment.StartUtc < x.EndUtc &&
                candidateSegment.EndUtc > x.StartUtc);

            if (ownDemand > availableCount)
                return true;
        }

        if (existingAppointments.Count == 0)
            return false;

        var existingServiceIds = existingAppointments
            .Select(x => x.ServiceId)
            .Distinct()
            .ToList();

        var existingUsages = await _dbContext.ServiceResourceUsages
            .AsNoTracking()
            .Where(x => existingServiceIds.Contains(x.ServiceId))
            .ToListAsync(cancellationToken);

        if (existingUsages.Count == 0)
            return false;

        var existingUsageResourceIds = existingUsages
            .Select(x => x.ResourceId)
            .Distinct()
            .ToList();

        var existingResources = await _dbContext.Resources
            .AsNoTracking()
            .Where(x =>
                existingUsageResourceIds.Contains(x.Id) &&
                x.BusinessId == businessId &&
                x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.ResourceGroupId,
                x.CreatesOccupancy,
                x.AllowParallelUsage
            })
            .ToListAsync(cancellationToken);

        var existingResourcesById = existingResources.ToDictionary(x => x.Id);

        foreach (var candidateSegment in candidateSegments)
        {
            var availableCount = poolCapacities[candidateSegment.PoolKey];

            var ownDemand = candidateSegments.Count(x =>
                x.PoolKey == candidateSegment.PoolKey &&
                candidateSegment.StartUtc < x.EndUtc &&
                candidateSegment.EndUtc > x.StartUtc);

            var overlappingExistingCount = 0;

            foreach (var appointment in existingAppointments)
            {
                var appointmentUsages = existingUsages
                    .Where(x => x.ServiceId == appointment.ServiceId);

                foreach (var usage in appointmentUsages)
                {
                    if (!existingResourcesById.TryGetValue(usage.ResourceId, out var resource))
                        continue;

                    if (!resource.CreatesOccupancy)
                        continue;

                    if (resource.AllowParallelUsage)
                        continue;

                    var existingPoolKey = GetResourcePoolKey(resource.Id, resource.ResourceGroupId);

                    if (existingPoolKey != candidateSegment.PoolKey)
                        continue;

                    var existingSegmentStartUtc = appointment.StartAtUtc.AddMinutes(usage.StartMinute);
                    var existingSegmentEndUtc = existingSegmentStartUtc.AddMinutes(usage.DurationMin);

                    var overlaps =
                        candidateSegment.StartUtc < existingSegmentEndUtc &&
                        candidateSegment.EndUtc > existingSegmentStartUtc;

                    if (overlaps)
                        overlappingExistingCount++;
                }
            }

            if (overlappingExistingCount + ownDemand > availableCount)
                return true;
        }

        return false;
    }



    private static string BuildUnavailableMessage(AppointmentAvailabilityResult result)
    {
        var reasons = new List<string>();

        if (result.HasBusinessHoursViolation && result.HasStaffHoursViolation)
            reasons.Add("izvan radnog vremena");

        else
        {
            if (result.HasBusinessHoursViolation)
                reasons.Add("izvan radnog vremena radnje");

            if (result.HasStaffHoursViolation)
                reasons.Add("izvan radnog vremena zaposlenog");
        }

        if (result.HasTimeOffConflict)
            reasons.Add("u periodu kada nije moguće zakazivanje");

        if (result.HasAppointmentConflict)
            reasons.Add("u terminu koji je već zauzet");

        if (result.HasResourceConflict)
            reasons.Add("zato što je resurs već zauzet");

        if (reasons.Count == 0)
            return "Izabrani termin trenutno nije dostupan.";

        return $"Izabrani termin nije dostupan jer je {string.Join(", ", reasons)}.";
    }

    private static string BuildReasonCode(AppointmentAvailabilityResult result)
    {
        if (result.HasBusinessHoursViolation && result.HasStaffHoursViolation)
            return "outside_business_and_staff_hours";

        if (result.HasBusinessHoursViolation)
            return "outside_business_hours";

        if (result.HasStaffHoursViolation)
            return "outside_staff_hours";

        if (result.HasTimeOffConflict && result.HasAppointmentConflict)
            return "timeoff_and_appointment_conflict";

        if (result.HasTimeOffConflict)
            return "timeoff_conflict";

        if (result.HasAppointmentConflict)
            return "appointment_conflict";

        if (result.HasResourceConflict)
            return "resource_conflict";

        return "not_available";
    }

    private DateTime LocalDateTimeToUtc(DateTime localDate, TimeSpan localTime)
    {
        var localDateTime = DateTime.SpecifyKind(localDate.Date.Add(localTime), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, _businessTimeZone);
    }

    private DateTime UtcToLocal(DateTime utcDateTime)
    {
        var normalizedUtc = utcDateTime.Kind == DateTimeKind.Utc
            ? utcDateTime
            : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc, _businessTimeZone);
    }

    private static TimeZoneInfo ResolveBusinessTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            if (timeZoneId == "Europe/Belgrade")
                return TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");

            throw;
        }
        catch (InvalidTimeZoneException)
        {
            if (timeZoneId == "Europe/Belgrade")
                return TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");

            throw;
        }
    }
}