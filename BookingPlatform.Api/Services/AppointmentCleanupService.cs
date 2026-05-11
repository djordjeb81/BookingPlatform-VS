using BookingPlatform.Domain.Appointments;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Services;

public sealed class AppointmentCleanupService
{
    private readonly BookingDbContext _dbContext;

    public AppointmentCleanupService(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> DeleteOldClosedAppointmentsAsync(CancellationToken cancellationToken)
    {
        var cutoffUtc = DateTime.UtcNow.AddMonths(-6);

        var closedStatuses = new[]
        {
            AppointmentStatus.Completed,
            AppointmentStatus.Cancelled,
            AppointmentStatus.Rejected,
            AppointmentStatus.NoShow
        };

        var appointmentIds = await _dbContext.Appointments
            .Where(x =>
                closedStatuses.Contains(x.Status) &&
                x.EndAtUtc < cutoffUtc)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (appointmentIds.Count == 0)
            return 0;

        var changeRequests = await _dbContext.AppointmentChangeRequests
            .Where(x => appointmentIds.Contains(x.AppointmentId))
            .ToListAsync(cancellationToken);

        var auditLogs = await _dbContext.AppointmentAuditLogs
            .Where(x => appointmentIds.Contains(x.AppointmentId))
            .ToListAsync(cancellationToken);

        var appointments = await _dbContext.Appointments
            .Where(x => appointmentIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (changeRequests.Count > 0)
            _dbContext.AppointmentChangeRequests.RemoveRange(changeRequests);

        if (auditLogs.Count > 0)
            _dbContext.AppointmentAuditLogs.RemoveRange(auditLogs);

        _dbContext.Appointments.RemoveRange(appointments);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return appointments.Count;
    }
}