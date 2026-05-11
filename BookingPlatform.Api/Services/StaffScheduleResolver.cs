using BookingPlatform.Domain.Scheduling;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Services;

public interface IStaffScheduleResolver
{
    Task<List<(TimeSpan Start, TimeSpan End)>> GetEffectiveIntervalsAsync(
        long staffMemberId,
        DateTime dateUtc,
        CancellationToken cancellationToken);
}

public sealed class StaffScheduleResolver : IStaffScheduleResolver
{
    private readonly BookingDbContext _dbContext;

    public StaffScheduleResolver(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<(TimeSpan Start, TimeSpan End)>> GetEffectiveIntervalsAsync(
        long staffMemberId,
        DateTime dateUtc,
        CancellationToken cancellationToken)
    {
        var date = dateUtc.Date;

        var overrideRule = await _dbContext.StaffScheduleOverrides
            .AsNoTracking()
            .Where(x => x.StaffMemberId == staffMemberId && x.Date == date)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (overrideRule is not null)
        {
            if (overrideRule.IsDayOff || overrideRule.OverrideType == ScheduleOverrideType.DayOff)
                return new List<(TimeSpan Start, TimeSpan End)>();

            if (overrideRule.StartTime.HasValue && overrideRule.EndTime.HasValue)
            {
                return new List<(TimeSpan Start, TimeSpan End)>
                {
                    (overrideRule.StartTime.Value, overrideRule.EndTime.Value)
                };
            }
        }

        var dayOfWeek = date.DayOfWeek switch
        {
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday => 4,
            DayOfWeek.Friday => 5,
            DayOfWeek.Saturday => 6,
            _ => 7
        };

        var weekType = GetWeekType(date);

        var weekSpecific = await _dbContext.StaffScheduleRules
            .AsNoTracking()
            .Where(x =>
                x.StaffMemberId == staffMemberId &&
                x.DayOfWeek == dayOfWeek &&
                x.WeekType == weekType &&
                x.IsActive)
            .OrderBy(x => x.StartTime)
            .ToListAsync(cancellationToken);

        if (weekSpecific.Count > 0)
        {
            return weekSpecific
                .Select(x => (x.StartTime, x.EndTime))
                .ToList();
        }

        var anyRules = await _dbContext.StaffScheduleRules
            .AsNoTracking()
            .Where(x =>
                x.StaffMemberId == staffMemberId &&
                x.DayOfWeek == dayOfWeek &&
                x.WeekType == ScheduleWeekType.Any &&
                x.IsActive)
            .OrderBy(x => x.StartTime)
            .ToListAsync(cancellationToken);

        return anyRules
            .Select(x => (x.StartTime, x.EndTime))
            .ToList();
    }

    private static ScheduleWeekType GetWeekType(DateTime date)
    {
        var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(date);
        return weekNumber % 2 == 0 ? ScheduleWeekType.WeekB : ScheduleWeekType.WeekA;
    }
}