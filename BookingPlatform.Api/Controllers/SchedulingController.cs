using BookingPlatform.Api.Helpers;
using BookingPlatform.Api.Services;
using BookingPlatform.Contracts.Scheduling;
using BookingPlatform.Domain.Appointments;
using BookingPlatform.Domain.Auth;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Security.Claims;


namespace BookingPlatform.Api.Controllers;



[ApiController]
[Route("api/[controller]")]
public sealed class SchedulingController : ControllerBase
{
    private readonly BookingDbContext _dbContext;
    private readonly IAppointmentSchedulingService _appointmentSchedulingService;
    private readonly TimeZoneInfo _businessTimeZone;
    public SchedulingController(
        BookingDbContext dbContext,
        IAppointmentSchedulingService appointmentSchedulingService,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _appointmentSchedulingService = appointmentSchedulingService;

        var timeZoneId = configuration["Scheduling:TimeZoneId"];

        if (string.IsNullOrWhiteSpace(timeZoneId))
            timeZoneId = "Europe/Belgrade";

        _businessTimeZone = ResolveBusinessTimeZone(timeZoneId);
    }

    [HttpPost("available-slots")]
    public async Task<ActionResult<List<AvailableSlotDto>>> SearchAvailableSlots(
     [FromBody] SearchAvailableSlotsRequest request,
     CancellationToken cancellationToken)
    {
        if (request.StaffMemberId is null)
            return BadRequest("Za v1 je StaffMemberId obavezan.");

        var staffServiceValidationError = await StaffServiceValidationHelper.ValidateStaffCanPerformServiceAsync(
            _dbContext,
            request.BusinessId,
            request.ServiceId,
            request.StaffMemberId,
            cancellationToken);

        if (staffServiceValidationError is not null)
            return BadRequest(staffServiceValidationError);

        var staffResourceValidationError = await StaffResourceValidationHelper.ValidateStaffCanUseResourceAsync(
            _dbContext,
            request.BusinessId,
            request.StaffMemberId,
            request.ResourceId,
            cancellationToken);

        if (staffResourceValidationError is not null)
            return BadRequest(staffResourceValidationError);

        var resourceValidationError = await ValidateRequestedResourceAsync(
            request.BusinessId,
            request.ServiceId,
            request.ResourceId,
            cancellationToken);

        if (resourceValidationError is not null)
            return BadRequest(resourceValidationError);

        var results = await BuildAvailableSlotsAsync(
            request.BusinessId,
            request.ServiceId,
            request.StaffMemberId.Value,
            request.ResourceId,
            request.Date,
            cancellationToken);

        return Ok(results);
    }

    [HttpPost("first-available")]
    public async Task<ActionResult<List<FirstAvailableResultDto>>> GetFirstAvailable(
        [FromBody] FirstAvailableSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SearchDays <= 0)
            return BadRequest("SearchDays mora biti veći od 0.");

        if (request.StaffMemberId.HasValue)
        {
            var staffServiceValidationError = await StaffServiceValidationHelper.ValidateStaffCanPerformServiceAsync(
                _dbContext,
                request.BusinessId,
                request.ServiceId,
                request.StaffMemberId,
                cancellationToken);

            if (staffServiceValidationError is not null)
                return BadRequest(staffServiceValidationError);

            var staffResourceValidationError = await StaffResourceValidationHelper.ValidateStaffCanUseResourceAsync(
                _dbContext,
                request.BusinessId,
                request.StaffMemberId,
                request.ResourceId,
                cancellationToken);

            if (staffResourceValidationError is not null)
                return BadRequest(staffResourceValidationError);
        }

        var resourceValidationError = await ValidateRequestedResourceAsync(
            request.BusinessId,
            request.ServiceId,
            request.ResourceId,
            cancellationToken);

        if (resourceValidationError is not null)
            return BadRequest(resourceValidationError);

        var startDate = (request.StartDate ?? UtcToLocal(DateTime.UtcNow).Date).Date;

        List<(long StaffId, string? StaffName)> staffCandidates;

        if (request.StaffMemberId.HasValue)
        {
            var staff = await _dbContext.StaffMembers
                .AsNoTracking()
                .Where(x =>
                    x.Id == request.StaffMemberId.Value &&
                    x.BusinessId == request.BusinessId &&
                    x.IsActive &&
                    x.IsBookable)
                .Select(x => new
                {
                    x.Id,
                    x.DisplayName
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (staff is null)
                return BadRequest("Izabrani radnik ne postoji.");

            staffCandidates = new List<(long StaffId, string? StaffName)>
        {
            (staff.Id, staff.DisplayName)
        };
        }
        else
        {
            if (request.ResourceId.HasValue)
            {
                var staffCandidatesRaw = await (
                    from staff in _dbContext.StaffMembers.AsNoTracking()
                    join staffService in _dbContext.StaffServiceAssignments.AsNoTracking()
                        on staff.Id equals staffService.StaffMemberId
                    join staffResource in _dbContext.StaffResourceAssignments.AsNoTracking()
                        on staff.Id equals staffResource.StaffMemberId
                    where staff.BusinessId == request.BusinessId
                          && staff.IsActive
                          && staff.IsBookable
                          && staffService.ServiceId == request.ServiceId
                          && staffResource.ResourceId == request.ResourceId.Value
                    orderby staff.DisplayName
                    select new
                    {
                        StaffId = staff.Id,
                        StaffName = staff.DisplayName
                    })
                    .Distinct()
                    .ToListAsync(cancellationToken);

                staffCandidates = staffCandidatesRaw
                    .Select(x => (
                        StaffId: x.StaffId,
                        StaffName: (string?)x.StaffName
                    ))
                    .ToList();
            }
            else
            {
                var staffCandidatesRaw = await (
                    from staff in _dbContext.StaffMembers.AsNoTracking()
                    join staffService in _dbContext.StaffServiceAssignments.AsNoTracking()
                        on staff.Id equals staffService.StaffMemberId
                    where staff.BusinessId == request.BusinessId
                          && staff.IsActive
                          && staff.IsBookable
                          && staffService.ServiceId == request.ServiceId
                    orderby staff.DisplayName
                    select new
                    {
                        StaffId = staff.Id,
                        StaffName = staff.DisplayName
                    })
                    .Distinct()
                    .ToListAsync(cancellationToken);

                staffCandidates = staffCandidatesRaw
                    .Select(x => (
                        StaffId: x.StaffId,
                        StaffName: (string?)x.StaffName
                    ))
                    .ToList();
            }

            if (staffCandidates.Count == 0)
                return Ok(new List<FirstAvailableResultDto>());
        }

        var found = new List<FirstAvailableResultDto>();

        for (var dayOffset = 0; dayOffset < request.SearchDays; dayOffset++)
        {
            var currentDate = startDate.AddDays(dayOffset);

            foreach (var staffCandidate in staffCandidates)
            {
                var slots = await BuildAvailableSlotsAsync(
                    request.BusinessId,
                    request.ServiceId,
                    staffCandidate.StaffId,
                    request.ResourceId,
                    currentDate,
                    cancellationToken);

                if (slots.Count == 0)
                    continue;

                var firstSlot = slots.First();

                found.Add(new FirstAvailableResultDto
                {
                    StaffMemberId = staffCandidate.StaffId,
                    StaffDisplayName = staffCandidate.StaffName,
                    StartAtUtc = firstSlot.StartAtUtc,
                    EndAtUtc = firstSlot.EndAtUtc,
                    StartLabel = firstSlot.StartLabel,
                    EndLabel = firstSlot.EndLabel,
                    DateLabel = UtcToLocal(firstSlot.StartAtUtc).ToString("yyyy-MM-dd")
                });
            }

            if (found.Count > 0)
            {
                return Ok(found
                    .OrderBy(x => x.StartAtUtc)
                    .ThenBy(x => x.StaffDisplayName)
                    .Take(5)
                    .ToList());
            }
        }

        return Ok(new List<FirstAvailableResultDto>());
    }


    [Authorize]
    [HttpGet("daily-calendar")]
    public async Task<ActionResult<List<DailyCalendarItemDto>>> GetDailyCalendar(
    [FromQuery] long businessId,
    [FromQuery] DateTime date,
    [FromQuery] long? staffMemberId,
    CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var targetDate = date.Date;
        var dayStartUtc = LocalDateTimeToUtc(targetDate, TimeSpan.Zero);
        var dayEndUtc = LocalDateTimeToUtc(targetDate.AddDays(1), TimeSpan.Zero);

        var appointmentsQuery = _dbContext.Appointments
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.Status != AppointmentStatus.Rejected &&
                x.Status != AppointmentStatus.Cancelled &&
                x.StartAtUtc < dayEndUtc &&
                x.EndAtUtc > dayStartUtc);

        if (staffMemberId.HasValue)
            appointmentsQuery = appointmentsQuery.Where(x => x.PrimaryStaffMemberId == staffMemberId.Value);

        var appointments = await appointmentsQuery
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync(cancellationToken);

        var blocksQuery = _dbContext.TimeOffBlocks
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.StartAtUtc < dayEndUtc &&
                x.EndAtUtc > dayStartUtc);

        if (staffMemberId.HasValue)
            blocksQuery = blocksQuery.Where(x => x.StaffMemberId == null || x.StaffMemberId == staffMemberId.Value);

        var blocks = await blocksQuery
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync(cancellationToken);

        var serviceIds = appointments
            .Select(x => x.ServiceId)
            .Distinct()
            .ToList();

        var staffIds = appointments
            .Where(x => x.PrimaryStaffMemberId.HasValue)
            .Select(x => x.PrimaryStaffMemberId!.Value)
            .Concat(blocks.Where(x => x.StaffMemberId.HasValue).Select(x => x.StaffMemberId!.Value))
            .Distinct()
            .ToList();

        var resourceIds = appointments
            .Where(x => x.ResourceId.HasValue)
            .Select(x => x.ResourceId!.Value)
            .Distinct()
            .ToList();

        var services = await _dbContext.Services
            .AsNoTracking()
            .Where(x => serviceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var staff = await _dbContext.StaffMembers
            .AsNoTracking()
            .Where(x => staffIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var resources = await _dbContext.Resources
            .AsNoTracking()
            .Where(x => resourceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var result = new List<DailyCalendarItemDto>();

        foreach (var appointment in appointments)
        {
            services.TryGetValue(appointment.ServiceId, out var serviceName);

            string? staffDisplayName = null;
            if (appointment.PrimaryStaffMemberId.HasValue &&
                staff.TryGetValue(appointment.PrimaryStaffMemberId.Value, out var staffName))
            {
                staffDisplayName = staffName;
            }

            string? resourceName = null;
            if (appointment.ResourceId.HasValue &&
                resources.TryGetValue(appointment.ResourceId.Value, out var foundResourceName))
            {
                resourceName = foundResourceName;
            }

            result.Add(new DailyCalendarItemDto
            {
                ItemType = "Appointment",
                AppointmentId = appointment.Id,
                BusinessId = appointment.BusinessId,
                StaffMemberId = appointment.PrimaryStaffMemberId,
                StaffDisplayName = staffDisplayName,
                ResourceId = appointment.ResourceId,
                ResourceName = resourceName,
                StartAtUtc = appointment.StartAtUtc,
                EndAtUtc = appointment.EndAtUtc,
                Title = serviceName ?? "Termin",
                Subtitle = appointment.Notes,
                CustomerName = appointment.CustomerName,
                CustomerPhone = appointment.CustomerPhone,
                AppointmentStatus = appointment.Status.ToString(),
                StartLabel = UtcToLocal(appointment.StartAtUtc).ToString("HH:mm"),
                EndLabel = UtcToLocal(appointment.EndAtUtc).ToString("HH:mm")
            });
        }

        foreach (var block in blocks)
        {
            string? staffDisplayName = null;
            if (block.StaffMemberId.HasValue &&
                staff.TryGetValue(block.StaffMemberId.Value, out var staffName))
            {
                staffDisplayName = staffName;
            }

            result.Add(new DailyCalendarItemDto
            {
                ItemType = "Block",
                BlockId = block.Id,
                BusinessId = block.BusinessId,
                StaffMemberId = block.StaffMemberId,
                StaffDisplayName = staffDisplayName,
                ResourceId = null,
                ResourceName = null,
                StartAtUtc = block.StartAtUtc,
                EndAtUtc = block.EndAtUtc,
                Title = block.Reason ?? "Blokirano vreme",
                Subtitle = null,
                CustomerName = null,
                CustomerPhone = null,
                AppointmentStatus = null,
                BlockType = block.BlockType.ToString(),
                StartLabel = UtcToLocal(block.StartAtUtc).ToString("HH:mm"),
                EndLabel = UtcToLocal(block.EndAtUtc).ToString("HH:mm")
            });
        }

        return Ok(result
            .OrderBy(x => x.StartAtUtc)
            .ThenBy(x => x.EndAtUtc)
            .ToList());
    }

    private async Task<string?> ValidateRequestedResourceAsync(
    long businessId,
    long serviceId,
    long? resourceId,
    CancellationToken cancellationToken)
    {
        var service = await _dbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == serviceId && x.BusinessId == businessId,
                cancellationToken);

        if (service is null)
            return "Izabrana usluga ne postoji.";

        var serviceResourceRequirements = await _dbContext.ServiceResourceRequirements
            .AsNoTracking()
            .Where(x => x.ServiceId == serviceId)
            .ToListAsync(cancellationToken);

        if (!resourceId.HasValue)
        {
            if (serviceResourceRequirements.Any(x => x.IsRequired))
                return "Za izabranu uslugu je potrebno izabrati resurs.";

            return null;
        }

        var resource = await _dbContext.Resources
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == resourceId.Value && x.BusinessId == businessId,
                cancellationToken);

        if (resource is null)
            return "Izabrani resurs ne postoji.";

        if (!resource.IsActive)
            return "Izabrani resurs nije aktivan.";

        var isAllowedForService = serviceResourceRequirements.Any(x => x.ResourceId == resourceId.Value);

        if (!isAllowedForService)
            return "Izabrani resurs nije dozvoljen za izabranu uslugu.";

        return null;
    }

    private long? TryGetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out var userId) ? userId : null;
    }

    private async Task<ActionResult?> EnsureBusinessReadAccessAsync(
        long businessId,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized("Korisnik nije autentifikovan.");

        var membership = await _dbContext.BusinessUserMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.AppUserId == userId.Value &&
                x.BusinessId == businessId &&
                x.IsActive,
                cancellationToken);

        if (membership is null)
            return Forbid();

        return null;
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

    private static DateTime GetNextCandidateStartAfterBusyEnd(
    DateTime busyEndUtc,
    DateTime gridStartUtc,
    int slotStepMinutes,
    int serviceDurationMin)
    {
        if (slotStepMinutes <= 0)
            slotStepMinutes = 30;

        if (busyEndUtc <= gridStartUtc)
            return gridStartUtc;

        var minutesFromGridStart = (busyEndUtc - gridStartUtc).TotalMinutes;
        var roundedSteps = Math.Ceiling(minutesFromGridStart / slotStepMinutes);
        var nextGridStartUtc = gridStartUtc.AddMinutes(roundedSteps * slotStepMinutes);

        var freeMinutesUntilNextGrid = (nextGridStartUtc - busyEndUtc).TotalMinutes;

        if (freeMinutesUntilNextGrid > 0 &&
            serviceDurationMin <= freeMinutesUntilNextGrid)
        {
            return busyEndUtc;
        }

        return nextGridStartUtc;
    }

    private static DateTime GetNextCandidateStartAfterBusyEnd(
    DateTime busyEndUtc,
    DateTime gridStartUtc,
    int slotStepMinutes,
    int serviceDurationMin,
    int gapBufferMinutes)
    {
        if (slotStepMinutes <= 0)
            slotStepMinutes = 30;

        if (gapBufferMinutes < 0)
            gapBufferMinutes = 0;

        if (busyEndUtc <= gridStartUtc)
            return gridStartUtc;

        var minutesFromGridStart = (busyEndUtc - gridStartUtc).TotalMinutes;
        var roundedSteps = Math.Ceiling(minutesFromGridStart / slotStepMinutes);
        var nextGridStartUtc = gridStartUtc.AddMinutes(roundedSteps * slotStepMinutes);

        if (busyEndUtc == nextGridStartUtc)
            return nextGridStartUtc;

        var gapCandidateStartUtc = busyEndUtc.AddMinutes(gapBufferMinutes);
        var freeMinutesUntilNextGrid = (nextGridStartUtc - gapCandidateStartUtc).TotalMinutes;

        if (freeMinutesUntilNextGrid > 0 &&
            serviceDurationMin <= freeMinutesUntilNextGrid)
        {
            return gapCandidateStartUtc;
        }

        return nextGridStartUtc;
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

    private async Task<List<(TimeSpan StartTime, TimeSpan EndTime)>> GetEffectiveStaffWorkingRangesAsync(
      BookingPlatform.Domain.Staff.StaffMember staff,
      DateTime targetDate,
      int dayOfWeek,
      CancellationToken cancellationToken)
    {
        var scheduleMode = (int)staff.ScheduleMode;

        if (scheduleMode == 0)
        {
            var fixedRuleRange = await GetFixedScheduleRuleRangeAsync(
                staff.Id,
                dayOfWeek,
                cancellationToken);

            if (fixedRuleRange is not null)
                return new List<(TimeSpan StartTime, TimeSpan EndTime)> { fixedRuleRange.Value };

            var legacyRange = await GetLegacyStaffWorkingHoursRangeAsync(
                staff.Id,
                dayOfWeek,
                cancellationToken);

            return legacyRange is null
                ? new List<(TimeSpan StartTime, TimeSpan EndTime)>()
                : new List<(TimeSpan StartTime, TimeSpan EndTime)> { legacyRange.Value };
        }

        if (scheduleMode == 1)
        {
            var shiftRange = await GetShiftScheduleRuleRangeAsync(
                staff.Id,
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
                staff.Id,
                dayOfWeek,
                cancellationToken);
        }

        var fallbackRange = await GetLegacyStaffWorkingHoursRangeAsync(
            staff.Id,
            dayOfWeek,
            cancellationToken);

        return fallbackRange is null
            ? new List<(TimeSpan StartTime, TimeSpan EndTime)>()
            : new List<(TimeSpan StartTime, TimeSpan EndTime)> { fallbackRange.Value };
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
    private static long GetResourcePoolKey(long resourceId, long? resourceGroupId)
    {
        // Ako resurs ima grupu, pool je grupa.
        // Ako nema grupu, resurs je sam svoja grupa.
        // Group key stavljamo kao negativan broj da se ne sudari sa ResourceId.
        return resourceGroupId.HasValue
            ? -resourceGroupId.Value
            : resourceId;
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

            foreach (var resourceId in existingSingleResources)
            {
                poolCapacities[resourceId] = 1;
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

        if (existingAppointments.Count == 0)
        {
            foreach (var segment in candidateSegments)
            {
                var availableCount = poolCapacities[segment.PoolKey];

                var ownDemand = candidateSegments.Count(x =>
                    x.PoolKey == segment.PoolKey &&
                    segment.StartUtc < x.EndUtc &&
                    segment.EndUtc > x.StartUtc);

                if (ownDemand > availableCount)
                    return true;
            }

            return false;
        }

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


    private async Task<List<AvailableSlotDto>> BuildAvailableSlotsAsync(
     long businessId,
     long serviceId,
     long staffMemberId,
     long? resourceId,
     DateTime date,
     CancellationToken cancellationToken)
    {
        var service = await _dbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == serviceId && x.BusinessId == businessId,
                cancellationToken);

        if (service is null)
            return new List<AvailableSlotDto>();

        var business = await _dbContext.Businesses
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == businessId && x.IsActive,
                cancellationToken);

        if (business is null)
            return new List<AvailableSlotDto>();

        var staff = await _dbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == staffMemberId && x.BusinessId == businessId,
                cancellationToken);

        if (staff is null)
            return new List<AvailableSlotDto>();

        var totalDurationMin = await _appointmentSchedulingService.GetTotalServiceDurationAsync(
            service.Id,
            service.EstimatedDurationMin,
            cancellationToken);

        var targetDate = date.Date;

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

        var businessHours = await _dbContext.BusinessWorkingHours
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.BusinessId == businessId && x.DayOfWeek == dayOfWeek,
                cancellationToken);

        if (businessHours is null || businessHours.IsClosed)
            return new List<AvailableSlotDto>();

        var staffWorkingRanges = await GetEffectiveStaffWorkingRangesAsync(
            staff,
            targetDate,
            dayOfWeek,
            cancellationToken);

        if (staffWorkingRanges.Count == 0)
            return new List<AvailableSlotDto>();

        var effectiveRanges = staffWorkingRanges
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
            .ToList();

        if (effectiveRanges.Count == 0)
            return new List<AvailableSlotDto>();

        var effectiveUtcRanges = effectiveRanges
            .Select(x => new
            {
                StartUtc = LocalDateTimeToUtc(targetDate, x.StartTime),
                EndUtc = LocalDateTimeToUtc(targetDate, x.EndTime)
            })
            .Where(x => x.EndUtc > x.StartUtc)
            .ToList();

        if (effectiveUtcRanges.Count == 0)
            return new List<AvailableSlotDto>();

        var dayStartUtc = effectiveUtcRanges.Min(x => x.StartUtc);
        var dayEndUtc = effectiveUtcRanges.Max(x => x.EndUtc);

        bool FitsInEffectiveWorkingRange(DateTime startUtc, DateTime endUtc)
        {
            return effectiveUtcRanges.Any(x =>
                startUtc >= x.StartUtc &&
                endUtc <= x.EndUtc);
        }

        Console.WriteLine("========== SCHEDULING TIME DEBUG ==========");
        Console.WriteLine($"TimeZone={_businessTimeZone.Id}");
        Console.WriteLine($"Input date={date:O} Kind={date.Kind}");
        Console.WriteLine($"Target local date={targetDate:yyyy-MM-dd} Kind={targetDate.Kind}");
        foreach (var range in effectiveRanges)
        {
            Console.WriteLine($"Effective range local={range.StartTime} - {range.EndTime}");
        }
        Console.WriteLine($"DayStartUtc={dayStartUtc:O} Kind={dayStartUtc.Kind}");
        Console.WriteLine($"DayEndUtc={dayEndUtc:O} Kind={dayEndUtc.Kind}");
        Console.WriteLine("===========================================");

        var appointments = await _dbContext.Appointments
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.PrimaryStaffMemberId == staffMemberId &&
                x.Status != AppointmentStatus.Cancelled &&
                x.Status != AppointmentStatus.Rejected &&
                x.StartAtUtc < dayEndUtc &&
                x.EndAtUtc > dayStartUtc)
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync(cancellationToken);

        List<Appointment> resourceAppointments;

        if (resourceId.HasValue)
        {
            var resource = await _dbContext.Resources
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == resourceId.Value && x.BusinessId == businessId,
                    cancellationToken);

            if (resource is not null && resource.CreatesOccupancy && !resource.AllowParallelUsage)
            {
                resourceAppointments = await _dbContext.Appointments
                    .AsNoTracking()
                    .Join(
                        _dbContext.Resources.AsNoTracking(),
                        appointment => appointment.ResourceId,
                        resourceItem => resourceItem.Id,
                        (appointment, resourceItem) => new { Appointment = appointment, Resource = resourceItem })
                    .Where(x =>
                        x.Appointment.BusinessId == businessId &&
                        x.Appointment.ResourceId == resourceId.Value &&
                        x.Appointment.Status != AppointmentStatus.Cancelled &&
                        x.Appointment.Status != AppointmentStatus.Rejected &&
                        x.Appointment.StartAtUtc < dayEndUtc &&
                        x.Appointment.EndAtUtc > dayStartUtc &&
                        x.Resource.CreatesOccupancy)
                    .OrderBy(x => x.Appointment.StartAtUtc)
                    .Select(x => x.Appointment)
                    .ToListAsync(cancellationToken);
            }
            else
            {
                resourceAppointments = new List<Appointment>();
            }
        }
        else
        {
            resourceAppointments = new List<Appointment>();
        }

        var blocks = await _dbContext.TimeOffBlocks
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                (x.StaffMemberId == null || x.StaffMemberId == staffMemberId) &&
                x.StartAtUtc < dayEndUtc &&
                x.EndAtUtc > dayStartUtc)
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync(cancellationToken);

        var slotStepMinutes = business.SlotIntervalMin > 0
            ? business.SlotIntervalMin
            : 30;
        const int gapBufferMinutes = 5;

        var candidateStarts = new HashSet<DateTime>();

        foreach (var range in effectiveUtcRanges)
        {
            for (var candidateStart = range.StartUtc;
                 candidateStart.AddMinutes(totalDurationMin) <= range.EndUtc;
                 candidateStart = candidateStart.AddMinutes(slotStepMinutes))
            {
                candidateStarts.Add(candidateStart);
            }
        }

        foreach (var appointment in appointments)
        {
            var candidateStartAfterAppointment = GetNextCandidateStartAfterBusyEnd(
                appointment.EndAtUtc,
                dayStartUtc,
                slotStepMinutes,
                totalDurationMin,
                gapBufferMinutes);

            var candidateEndAfterAppointment = candidateStartAfterAppointment.AddMinutes(totalDurationMin);

            if (FitsInEffectiveWorkingRange(candidateStartAfterAppointment, candidateEndAfterAppointment))
            {
                candidateStarts.Add(candidateStartAfterAppointment);
            }
        }

        foreach (var block in blocks)
        {
            var candidateStartAfterBlock = GetNextCandidateStartAfterBusyEnd(
                block.EndAtUtc,
                dayStartUtc,
                slotStepMinutes,
                totalDurationMin,
                gapBufferMinutes);

            var candidateEndAfterBlock = candidateStartAfterBlock.AddMinutes(totalDurationMin);

            if (FitsInEffectiveWorkingRange(candidateStartAfterBlock, candidateEndAfterBlock))
            {
                candidateStarts.Add(candidateStartAfterBlock);
            }
        }

        foreach (var resourceAppointment in resourceAppointments)
        {
            var candidateStartAfterResourceAppointment = GetNextCandidateStartAfterBusyEnd(
                resourceAppointment.EndAtUtc,
                dayStartUtc,
                slotStepMinutes,
                totalDurationMin,
                gapBufferMinutes);

            var candidateEndAfterResourceAppointment = candidateStartAfterResourceAppointment.AddMinutes(totalDurationMin);

            if (FitsInEffectiveWorkingRange(candidateStartAfterResourceAppointment, candidateEndAfterResourceAppointment))
            {
                candidateStarts.Add(candidateStartAfterResourceAppointment);
            }
        }

        var results = new List<AvailableSlotDto>();

        foreach (var candidateStart in candidateStarts.OrderBy(x => x))
        {
            var candidateEnd = candidateStart.AddMinutes(totalDurationMin);

            if (!FitsInEffectiveWorkingRange(candidateStart, candidateEnd))
                continue;

            var hasAppointmentConflict = appointments.Any(a =>
                candidateStart < a.EndAtUtc && candidateEnd > a.StartAtUtc);

            if (hasAppointmentConflict)
                continue;

            var hasResourceConflict = resourceAppointments.Any(a =>
                candidateStart < a.EndAtUtc && candidateEnd > a.StartAtUtc);

            if (hasResourceConflict)
                continue;

            var availability = await _appointmentSchedulingService.CheckAvailabilityAsync(
                businessId,
                serviceId,
                staffMemberId,
                resourceId,
                candidateStart,
                candidateEnd,
                ignoreAppointmentId: null,
                ignoreWorkingHours: true,
                ignoreTimeOffBlocks: true,
                ignoreAppointmentConflicts: false,
                cancellationToken);

            if (!availability.IsAvailable)
                continue;

            var hasBlockConflict = blocks.Any(b =>
                candidateStart < b.EndAtUtc && candidateEnd > b.StartAtUtc);

            if (hasBlockConflict)
                continue;

            var candidateStartLocal = UtcToLocal(candidateStart);
            var candidateEndLocal = UtcToLocal(candidateEnd);

            results.Add(new AvailableSlotDto
            {
                StartAtUtc = DateTime.SpecifyKind(candidateStart, DateTimeKind.Utc),
                EndAtUtc = DateTime.SpecifyKind(candidateEnd, DateTimeKind.Utc),
                StartLabel = candidateStartLocal.ToString("HH:mm"),
                EndLabel = candidateEndLocal.ToString("HH:mm")
            });
        }

        return results;
    }



}