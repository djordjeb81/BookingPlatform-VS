using BookingPlatform.Contracts.Common;
using BookingPlatform.Contracts.Scheduling;
using BookingPlatform.Domain.Scheduling;
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
public sealed class StaffScheduleRulesController : ApiControllerBase
{
    public StaffScheduleRulesController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<StaffScheduleRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<StaffScheduleRuleDto>>> GetAll(
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
                StartTime = x.StartTime.ToString(@"hh\:mm"),
                EndTime = x.EndTime.ToString(@"hh\:mm"),
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("replace")]
    [ProducesResponseType(typeof(List<StaffScheduleRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<StaffScheduleRuleDto>>> Replace(
        [FromBody] ReplaceStaffScheduleRulesRequest request,
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

        var validationError = ValidateRules(request.Rules);
        if (validationError is not null)
            return BadRequest(validationError);

        var parsedRules = new List<StaffScheduleRule>();

        foreach (var row in request.Rules)
        {
            var startTime = TimeSpan.Parse(row.StartTime);
            var endTime = TimeSpan.Parse(row.EndTime);

            parsedRules.Add(new StaffScheduleRule
            {
                StaffMemberId = request.StaffMemberId,
                DayOfWeek = row.DayOfWeek,
                WeekType = (ScheduleWeekType)row.WeekType,
                StartTime = startTime,
                EndTime = endTime,
                IsActive = row.IsActive,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }

        var existing = await DbContext.StaffScheduleRules
            .Where(x => x.StaffMemberId == request.StaffMemberId)
            .ToListAsync(cancellationToken);

        DbContext.StaffScheduleRules.RemoveRange(existing);
        DbContext.StaffScheduleRules.AddRange(parsedRules);

        await DbContext.SaveChangesAsync(cancellationToken);

        var result = parsedRules
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.WeekType)
            .ThenBy(x => x.StartTime)
            .Select(x => new StaffScheduleRuleDto
            {
                Id = x.Id,
                StaffMemberId = x.StaffMemberId,
                DayOfWeek = x.DayOfWeek,
                WeekType = (int)x.WeekType,
                StartTime = x.StartTime.ToString(@"hh\:mm"),
                EndTime = x.EndTime.ToString(@"hh\:mm"),
                IsActive = x.IsActive
            })
            .ToList();

        return Ok(result);
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var rule = await DbContext.StaffScheduleRules
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (rule is null)
            return NotFound("Pravilo rasporeda ne postoji.");

        var staff = await DbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == rule.StaffMemberId, cancellationToken);

        if (staff is null)
            return BadRequest("Zaposleni ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(staff.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        DbContext.StaffScheduleRules.Remove(rule);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static string? ValidateRules(List<StaffScheduleRuleRowDto> rules)
    {
        foreach (var row in rules)
        {
            if (row.DayOfWeek < 1 || row.DayOfWeek > 7)
                return "DayOfWeek mora biti između 1 i 7.";

            if (row.WeekType < 0 || row.WeekType > 2)
                return "WeekType mora biti Any, WeekA ili WeekB.";

            if (!TimeSpan.TryParse(row.StartTime, out var startTime))
                return "StartTime nije validan.";

            if (!TimeSpan.TryParse(row.EndTime, out var endTime))
                return "EndTime nije validan.";

            if (endTime <= startTime)
                return "EndTime mora biti posle StartTime.";
        }

        var groupedByDay = rules
            .GroupBy(x => x.DayOfWeek);

        foreach (var dayGroup in groupedByDay)
        {
            var hasAny = dayGroup.Any(x => x.WeekType == (int)ScheduleWeekType.Any);
            var hasSpecific = dayGroup.Any(x =>
                x.WeekType == (int)ScheduleWeekType.WeekA ||
                x.WeekType == (int)ScheduleWeekType.WeekB);

            if (hasAny && hasSpecific)
                return $"Za dan {dayGroup.Key} nije dozvoljeno mešanje Any sa WeekA/WeekB pravilima.";

            var groupedByWeekType = dayGroup
                .GroupBy(x => x.WeekType);

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
}