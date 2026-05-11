using BookingPlatform.Contracts.Common;
using BookingPlatform.Contracts.Scheduling;
using BookingPlatform.Domain.Scheduling;
using BookingPlatform.Domain.Staff;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[Route("api/[controller]")]
public sealed class StaffSchedulesController : ApiControllerBase
{
    public StaffSchedulesController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    public async Task<ActionResult<List<StaffScheduleRuleDto>>> GetRules(
        [FromQuery] long staffMemberId,
        CancellationToken cancellationToken)
    {
        var staff = await DbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == staffMemberId, cancellationToken);

        if (staff is null)
            return NotFound("Zaposleni ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(staff.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var items = await DbContext.StaffScheduleRules
            .AsNoTracking()
            .Where(x => x.StaffMemberId == staffMemberId)
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.WeekType)
            .ThenBy(x => x.StartTime)
            .Select(x => new StaffScheduleRuleDto
            {
                Id = x.Id,
                StaffMemberId = x.StaffMemberId,
                DayOfWeek = x.DayOfWeek,
                WeekType = (int)x.WeekType,
                SegmentType = (int)x.SegmentType,
                StartTime = x.StartTime.ToString(@"hh\:mm"),
                EndTime = x.EndTime.ToString(@"hh\:mm"),
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("replace")]
    public async Task<ActionResult<List<StaffScheduleRuleDto>>> ReplaceRules(
        [FromBody] ReplaceStaffScheduleRulesRequest request,
        CancellationToken cancellationToken)
    {
        var staff = await DbContext.StaffMembers
            .FirstOrDefaultAsync(x => x.Id == request.StaffMemberId, cancellationToken);

        if (staff is null)
            return NotFound("Zaposleni ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(staff.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var validationError = ValidateScheduleRules((StaffScheduleMode)request.ScheduleMode, request.Rules);
        if (validationError is not null)
            return BadRequest(validationError);

        staff.ScheduleMode = (StaffScheduleMode)request.ScheduleMode;
        staff.UpdatedAtUtc = DateTime.UtcNow;

        var existing = await DbContext.StaffScheduleRules
            .Where(x => x.StaffMemberId == request.StaffMemberId)
            .ToListAsync(cancellationToken);

        DbContext.StaffScheduleRules.RemoveRange(existing);

        var now = DateTime.UtcNow;
        var entities = request.Rules.Select(x => new StaffScheduleRule
        {
            StaffMemberId = request.StaffMemberId,
            DayOfWeek = x.DayOfWeek,
            WeekType = (ScheduleWeekType)x.WeekType,
            SegmentType = (ScheduleSegmentType)x.SegmentType,
            StartTime = TimeSpan.Parse(x.StartTime),
            EndTime = TimeSpan.Parse(x.EndTime),
            IsActive = x.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }).ToList();

        DbContext.StaffScheduleRules.AddRange(entities);
        await DbContext.SaveChangesAsync(cancellationToken);

        var result = entities
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.WeekType)
            .ThenBy(x => x.StartTime)
            .Select(x => new StaffScheduleRuleDto
            {
                Id = x.Id,
                StaffMemberId = x.StaffMemberId,
                DayOfWeek = x.DayOfWeek,
                WeekType = (int)x.WeekType,
                SegmentType = (int)x.SegmentType,
                StartTime = x.StartTime.ToString(@"hh\:mm"),
                EndTime = x.EndTime.ToString(@"hh\:mm"),
                IsActive = x.IsActive
            })
            .ToList();

        return Ok(result);
    }

    [HttpGet("overrides")]
    public async Task<ActionResult<List<StaffScheduleOverrideDto>>> GetOverrides(
        [FromQuery] long staffMemberId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var staff = await DbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == staffMemberId, cancellationToken);

        if (staff is null)
            return NotFound("Zaposleni ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(staff.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var fromUtc = NormalizeUtcDate(from);
        var toUtc = NormalizeUtcDate(to);

        var query = DbContext.StaffScheduleOverrides
            .AsNoTracking()
            .Where(x => x.StaffMemberId == staffMemberId);

        if (fromUtc.HasValue)
            query = query.Where(x => x.Date >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(x => x.Date <= toUtc.Value);

        var items = await query
            .OrderBy(x => x.Date)
            .Select(x => new StaffScheduleOverrideDto
            {
                Id = x.Id,
                StaffMemberId = x.StaffMemberId,
                Date = x.Date,
                OverrideType = (int)x.OverrideType,
                ShiftType = x.ShiftType.HasValue ? (int)x.ShiftType.Value : null,
                StartTime = x.StartTime.HasValue ? x.StartTime.Value.ToString(@"hh\:mm") : null,
                EndTime = x.EndTime.HasValue ? x.EndTime.Value.ToString(@"hh\:mm") : null,
                IsDayOff = x.IsDayOff,
                Reason = x.Reason
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("overrides")]
    public async Task<ActionResult<StaffScheduleOverrideDto>> CreateOverride(
        [FromBody] CreateOrUpdateStaffScheduleOverrideRequest request,
        CancellationToken cancellationToken)
    {
        var staff = await DbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.StaffMemberId, cancellationToken);

        if (staff is null)
            return NotFound("Zaposleni ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(staff.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var validationError = ValidateScheduleOverride(request);
        if (validationError is not null)
            return BadRequest(validationError);

        var overrideDateUtc = NormalizeUtcDate(request.Date);
        if (!overrideDateUtc.HasValue)
            return BadRequest("Date je obavezan.");

        var entity = new StaffScheduleOverride
        {
            StaffMemberId = request.StaffMemberId,
            Date = overrideDateUtc.Value,
            OverrideType = (ScheduleOverrideType)request.OverrideType,
            ShiftType = request.ShiftType.HasValue ? (ScheduleSegmentType)request.ShiftType.Value : null,
            StartTime = string.IsNullOrWhiteSpace(request.StartTime) ? null : TimeSpan.Parse(request.StartTime),
            EndTime = string.IsNullOrWhiteSpace(request.EndTime) ? null : TimeSpan.Parse(request.EndTime),
            IsDayOff = request.IsDayOff,
            Reason = request.Reason?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        DbContext.StaffScheduleOverrides.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new StaffScheduleOverrideDto
        {
            Id = entity.Id,
            StaffMemberId = entity.StaffMemberId,
            Date = entity.Date,
            OverrideType = (int)entity.OverrideType,
            ShiftType = entity.ShiftType.HasValue ? (int)entity.ShiftType.Value : null,
            StartTime = entity.StartTime.HasValue ? entity.StartTime.Value.ToString(@"hh\:mm") : null,
            EndTime = entity.EndTime.HasValue ? entity.EndTime.Value.ToString(@"hh\:mm") : null,
            IsDayOff = entity.IsDayOff,
            Reason = entity.Reason
        });
    }

    [HttpPut("overrides/{id:long}")]
    public async Task<ActionResult<StaffScheduleOverrideDto>> UpdateOverride(
        [FromRoute] long id,
        [FromBody] CreateOrUpdateStaffScheduleOverrideRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.StaffScheduleOverrides
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Izuzetak rasporeda ne postoji.");

        var staff = await DbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.StaffMemberId, cancellationToken);

        if (staff is null)
            return NotFound("Zaposleni ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(staff.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var validationError = ValidateScheduleOverride(request);
        if (validationError is not null)
            return BadRequest(validationError);

        var overrideDateUtc = NormalizeUtcDate(request.Date);
        if (!overrideDateUtc.HasValue)
            return BadRequest("Date je obavezan.");

        entity.StaffMemberId = request.StaffMemberId;
        entity.Date = overrideDateUtc.Value;
        entity.OverrideType = (ScheduleOverrideType)request.OverrideType;
        entity.ShiftType = request.ShiftType.HasValue ? (ScheduleSegmentType)request.ShiftType.Value : null;
        entity.StartTime = string.IsNullOrWhiteSpace(request.StartTime) ? null : TimeSpan.Parse(request.StartTime);
        entity.EndTime = string.IsNullOrWhiteSpace(request.EndTime) ? null : TimeSpan.Parse(request.EndTime);
        entity.IsDayOff = request.IsDayOff;
        entity.Reason = request.Reason?.Trim();
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new StaffScheduleOverrideDto
        {
            Id = entity.Id,
            StaffMemberId = entity.StaffMemberId,
            Date = entity.Date,
            OverrideType = (int)entity.OverrideType,
            ShiftType = entity.ShiftType.HasValue ? (int)entity.ShiftType.Value : null,
            StartTime = entity.StartTime.HasValue ? entity.StartTime.Value.ToString(@"hh\:mm") : null,
            EndTime = entity.EndTime.HasValue ? entity.EndTime.Value.ToString(@"hh\:mm") : null,
            IsDayOff = entity.IsDayOff,
            Reason = entity.Reason
        });
    }

    [HttpDelete("overrides/{id:long}")]
    public async Task<ActionResult> DeleteOverride(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.StaffScheduleOverrides
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Izuzetak rasporeda ne postoji.");

        var staff = await DbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == entity.StaffMemberId, cancellationToken);

        if (staff is null)
            return BadRequest("Zaposleni ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(staff.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        DbContext.StaffScheduleOverrides.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static DateTime? NormalizeUtcDate(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        var date = value.Value.Date;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => DateTime.SpecifyKind(date, DateTimeKind.Utc),
            DateTimeKind.Local => DateTime.SpecifyKind(value.Value.ToUniversalTime().Date, DateTimeKind.Utc),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(date, DateTimeKind.Utc),
            _ => DateTime.SpecifyKind(date, DateTimeKind.Utc)
        };
    }

    private static string? ValidateScheduleRules(
        StaffScheduleMode scheduleMode,
        List<StaffScheduleRuleRowDto> rules)
    {
        foreach (var row in rules)
        {
            if (row.DayOfWeek < 1 || row.DayOfWeek > 7)
                return "DayOfWeek mora biti između 1 i 7.";

            if (row.WeekType < 0 || row.WeekType > 2)
                return "WeekType mora biti Any, WeekA ili WeekB.";

            if (row.SegmentType < 0 || row.SegmentType > 4)
                return "SegmentType nije validan.";

            if (!TimeSpan.TryParse(row.StartTime, out var startTime))
                return "StartTime nije validan.";

            if (!TimeSpan.TryParse(row.EndTime, out var endTime))
                return "EndTime nije validan.";

            if (endTime <= startTime)
                return "EndTime mora biti posle StartTime.";

            if (scheduleMode == StaffScheduleMode.Fixed && row.SegmentType != (int)ScheduleSegmentType.Fixed)
                return "Za Fixed režim dozvoljen je samo SegmentType = Fixed.";

            if (scheduleMode == StaffScheduleMode.Shift &&
                row.SegmentType != (int)ScheduleSegmentType.Shift1 &&
                row.SegmentType != (int)ScheduleSegmentType.Shift2)
                return "Za Shift režim dozvoljeni su samo Shift1 i Shift2.";

            if (scheduleMode == StaffScheduleMode.SplitShift &&
                row.SegmentType != (int)ScheduleSegmentType.Split1 &&
                row.SegmentType != (int)ScheduleSegmentType.Split2)
                return "Za SplitShift režim dozvoljeni su samo Split1 i Split2.";
        }

        var groupedByDay = rules.GroupBy(x => x.DayOfWeek);

        foreach (var dayGroup in groupedByDay)
        {
            var hasAny = dayGroup.Any(x => x.WeekType == (int)ScheduleWeekType.Any);
            var hasSpecific = dayGroup.Any(x =>
                x.WeekType == (int)ScheduleWeekType.WeekA ||
                x.WeekType == (int)ScheduleWeekType.WeekB);

            if (hasAny && hasSpecific)
                return $"Za dan {dayGroup.Key} nije dozvoljeno mešanje Any sa WeekA/WeekB pravilima.";

            var groupedByWeekType = dayGroup.GroupBy(x => x.WeekType);

            foreach (var weekGroup in groupedByWeekType)
            {
                var intervals = weekGroup
                    .Select(x => new
                    {
                        Start = TimeSpan.Parse(x.StartTime),
                        End = TimeSpan.Parse(x.EndTime)
                    })
                    .OrderBy(x => x.Start)
                    .ToList();

                for (var i = 1; i < intervals.Count; i++)
                {
                    if (intervals[i].Start < intervals[i - 1].End)
                        return $"Postoje preklapajući intervali za dan {dayGroup.Key}.";
                }
            }
        }

        return null;
    }

    private static string? ValidateScheduleOverride(CreateOrUpdateStaffScheduleOverrideRequest request)
    {
        if (request.OverrideType < 0 || request.OverrideType > 4)
            return "OverrideType nije validan.";

        if (request.IsDayOff || request.OverrideType == (int)ScheduleOverrideType.DayOff)
            return null;

        if (!string.IsNullOrWhiteSpace(request.StartTime) && !TimeSpan.TryParse(request.StartTime, out _))
            return "StartTime nije validan.";

        if (!string.IsNullOrWhiteSpace(request.EndTime) && !TimeSpan.TryParse(request.EndTime, out _))
            return "EndTime nije validan.";

        if (!string.IsNullOrWhiteSpace(request.StartTime) &&
            !string.IsNullOrWhiteSpace(request.EndTime))
        {
            var start = TimeSpan.Parse(request.StartTime);
            var end = TimeSpan.Parse(request.EndTime);

            if (end <= start)
                return "EndTime mora biti posle StartTime.";
        }

        if (request.ShiftType.HasValue)
        {
            var shiftType = request.ShiftType.Value;
            if (shiftType < 0 || shiftType > 4)
                return "ShiftType nije validan.";
        }

        return null;
    }
}