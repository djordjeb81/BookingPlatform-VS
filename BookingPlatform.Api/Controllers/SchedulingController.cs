using BookingPlatform.Contracts.Scheduling;
using BookingPlatform.Domain.Appointments;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SchedulingController : ControllerBase
{
    private readonly BookingDbContext _dbContext;

    public SchedulingController(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost("available-slots")]
    public async Task<ActionResult<List<AvailableSlotDto>>> SearchAvailableSlots(
    [FromBody] SearchAvailableSlotsRequest request,
    CancellationToken cancellationToken)
    {
        if (request.StaffMemberId is null)
            return BadRequest("Za v1 je StaffMemberId obavezan.");

        var results = await BuildAvailableSlotsAsync(
            request.BusinessId,
            request.ServiceId,
            request.StaffMemberId.Value,
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

        var startDate = (request.StartDate ?? DateTime.UtcNow.Date).Date;

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
                .Select(x => new { x.Id, x.DisplayName })
                .FirstOrDefaultAsync(cancellationToken);

            if (staff is null)
                return BadRequest("Staff member ne postoji.");

            staffCandidates = new List<(long StaffId, string? StaffName)>
        {
            (staff.Id, staff.DisplayName)
        };
        }
        else
        {
            staffCandidates = await _dbContext.StaffMembers
                .AsNoTracking()
                .Where(x =>
                    x.BusinessId == request.BusinessId &&
                    x.IsActive &&
                    x.IsBookable)
                .OrderBy(x => x.DisplayName)
                .Select(x => new ValueTuple<long, string?>(x.Id, x.DisplayName))
                .ToListAsync(cancellationToken);

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
                    DateLabel = firstSlot.StartAtUtc.ToString("yyyy-MM-dd")
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

    [HttpGet("daily-calendar")]
    public async Task<ActionResult<List<DailyCalendarItemDto>>> GetDailyCalendar(
    [FromQuery] long businessId,
    [FromQuery] DateTime date,
    [FromQuery] long? staffMemberId,
    CancellationToken cancellationToken)
    {
        var targetDate = date.Date;
        var dayStartUtc = targetDate;
        var dayEndUtc = targetDate.AddDays(1);

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

        var services = await _dbContext.Services
            .AsNoTracking()
            .Where(x => serviceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var staff = await _dbContext.StaffMembers
            .AsNoTracking()
            .Where(x => staffIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

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

            result.Add(new DailyCalendarItemDto
            {
                ItemType = "Appointment",
                AppointmentId = appointment.Id,
                BusinessId = appointment.BusinessId,
                StaffMemberId = appointment.PrimaryStaffMemberId,
                StaffDisplayName = staffDisplayName,
                StartAtUtc = appointment.StartAtUtc,
                EndAtUtc = appointment.EndAtUtc,
                Title = serviceName ?? "Termin",
                Subtitle = appointment.Notes,
                CustomerName = appointment.CustomerName,
                CustomerPhone = appointment.CustomerPhone,
                AppointmentStatus = appointment.Status.ToString(),
                StartLabel = appointment.StartAtUtc.ToString("HH:mm"),
                EndLabel = appointment.EndAtUtc.ToString("HH:mm")
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
                StartAtUtc = block.StartAtUtc,
                EndAtUtc = block.EndAtUtc,
                Title = block.Reason ?? "Blokirano vreme",
                Subtitle = null,
                CustomerName = null,
                CustomerPhone = null,
                AppointmentStatus = null,
                BlockType = block.BlockType.ToString(),
                StartLabel = block.StartAtUtc.ToString("HH:mm"),
                EndLabel = block.EndAtUtc.ToString("HH:mm")
            });
        }

        return Ok(result
            .OrderBy(x => x.StartAtUtc)
            .ThenBy(x => x.EndAtUtc)
            .ToList());
    }

    private async Task<List<AvailableSlotDto>> BuildAvailableSlotsAsync(
    long businessId,
    long serviceId,
    long staffMemberId,
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

        var staff = await _dbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == staffMemberId && x.BusinessId == businessId,
                cancellationToken);

        if (staff is null)
            return new List<AvailableSlotDto>();

        var totalDurationMin = await GetTotalServiceDurationAsync(
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

        var staffHours = await _dbContext.StaffWorkingHours
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.StaffMemberId == staffMemberId && x.DayOfWeek == dayOfWeek,
                cancellationToken);

        if (staffHours is null || staffHours.IsClosed)
            return new List<AvailableSlotDto>();

        var effectiveStart = businessHours.StartTime > staffHours.StartTime
            ? businessHours.StartTime
            : staffHours.StartTime;

        var effectiveEnd = businessHours.EndTime < staffHours.EndTime
            ? businessHours.EndTime
            : staffHours.EndTime;

        if (effectiveEnd <= effectiveStart)
            return new List<AvailableSlotDto>();

        var dayStartUtc = targetDate.Add(effectiveStart);
        var dayEndUtc = targetDate.Add(effectiveEnd);

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

        var blocks = await _dbContext.TimeOffBlocks
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                (x.StaffMemberId == null || x.StaffMemberId == staffMemberId) &&
                x.StartAtUtc < dayEndUtc &&
                x.EndAtUtc > dayStartUtc)
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync(cancellationToken);

        var results = new List<AvailableSlotDto>();
        const int slotStepMinutes = 5;

        for (var candidateStart = dayStartUtc;
             candidateStart.AddMinutes(totalDurationMin) <= dayEndUtc;
             candidateStart = candidateStart.AddMinutes(slotStepMinutes))
        {
            var candidateEnd = candidateStart.AddMinutes(totalDurationMin);

            var hasAppointmentConflict = appointments.Any(a =>
                candidateStart < a.EndAtUtc && candidateEnd > a.StartAtUtc);

            if (hasAppointmentConflict)
                continue;

            var hasBlockConflict = blocks.Any(b =>
                candidateStart < b.EndAtUtc && candidateEnd > b.StartAtUtc);

            if (hasBlockConflict)
                continue;

            results.Add(new AvailableSlotDto
            {
                StartAtUtc = candidateStart,
                EndAtUtc = candidateEnd,
                StartLabel = candidateStart.ToString("HH:mm"),
                EndLabel = candidateEnd.ToString("HH:mm")
            });
        }

        return results;
    }

    private async Task<int> GetTotalServiceDurationAsync(
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
}