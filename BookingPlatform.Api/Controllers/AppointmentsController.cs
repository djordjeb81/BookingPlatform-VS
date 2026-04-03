using BookingPlatform.Contracts.Appointments;
using BookingPlatform.Domain.Appointments;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace BookingPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly BookingDbContext _dbContext;

    public AppointmentsController(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<object>>> GetAll(
        [FromQuery] long? businessId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Appointments.AsNoTracking();

        if (businessId.HasValue)
            query = query.Where(x => x.BusinessId == businessId.Value);

        var items = await query
            .OrderBy(x => x.StartAtUtc)
            .Select(x => new
            {
                x.Id,
                x.BusinessId,
                x.ServiceId,
                x.PrimaryStaffMemberId,
                x.CustomerName,
                x.CustomerPhone,
                x.Status,
                x.StartAtUtc,
                x.EndAtUtc,
                x.Notes
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create(
    [FromBody] CreateAppointmentRequest request,
    CancellationToken cancellationToken)
    {
        var service = await _dbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.ServiceId && x.BusinessId == request.BusinessId,
                cancellationToken);

        if (service is null)
            return BadRequest("Service ne postoji.");

        if (request.PrimaryStaffMemberId.HasValue)
        {
            var staffExists = await _dbContext.StaffMembers
                .AnyAsync(
                    x => x.Id == request.PrimaryStaffMemberId.Value && x.BusinessId == request.BusinessId,
                    cancellationToken);

            if (!staffExists)
                return BadRequest("Staff member ne postoji.");
        }

        var totalDurationMin = await GetTotalServiceDurationAsync(
            service.Id,
            service.EstimatedDurationMin,
            cancellationToken);

        var proposedStart = request.StartAtUtc;
        var proposedEnd = request.StartAtUtc.AddMinutes(totalDurationMin);

        var hasConflict = await _dbContext.Appointments
            .AsNoTracking()
            .AnyAsync(x =>
                x.BusinessId == request.BusinessId &&
                x.PrimaryStaffMemberId == request.PrimaryStaffMemberId &&
                x.Status != AppointmentStatus.Cancelled &&
                x.Status != AppointmentStatus.Rejected &&
                x.StartAtUtc < proposedEnd &&
                x.EndAtUtc > proposedStart,
                cancellationToken);

        if (hasConflict)
            return BadRequest("Termin nije dostupan.");

        var now = DateTime.UtcNow;

        var appointment = new Appointment
        {
            BusinessId = request.BusinessId,
            ServiceId = request.ServiceId,
            PrimaryStaffMemberId = request.PrimaryStaffMemberId,
            CustomerName = request.CustomerName.Trim(),
            CustomerPhone = request.CustomerPhone.Trim(),
            StartAtUtc = proposedStart,
            EndAtUtc = proposedEnd,
            Status = AppointmentStatus.PendingApproval,
            Notes = request.Notes?.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.Appointments.Add(appointment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var changeRequest = new AppointmentChangeRequest
        {
            AppointmentId = appointment.Id,
            RequestType = AppointmentChangeRequestType.NewBookingRequest,
            Status = AppointmentChangeRequestStatus.Pending,
            InitiatedBy = ChangeInitiatorType.Customer,
            OriginalStartAtUtc = proposedStart,
            OriginalEndAtUtc = proposedEnd,
            ProposedStartAtUtc = proposedStart,
            ProposedEndAtUtc = proposedEnd,
            Reason = "New booking request",
            Message = request.Notes?.Trim(),
            ExpiresAtUtc = now.AddMinutes(30),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.AppointmentChangeRequests.Add(changeRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            appointment.Id,
            appointment.BusinessId,
            appointment.ServiceId,
            appointment.PrimaryStaffMemberId,
            appointment.CustomerName,
            appointment.CustomerPhone,
            appointment.Status,
            appointment.StartAtUtc,
            appointment.EndAtUtc,
            ChangeRequestId = changeRequest.Id,
            ChangeRequestStatus = changeRequest.Status
        });
    }

    [HttpPost("approve")]
    public async Task<ActionResult<object>> Approve(
    [FromBody] ApproveAppointmentRequest request,
    CancellationToken cancellationToken)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            return NotFound("Appointment ne postoji.");

        if (appointment.Status != AppointmentStatus.PendingApproval)
            return BadRequest("Appointment nije u statusu čekanja potvrde.");

        appointment.Status = AppointmentStatus.Confirmed;
        appointment.UpdatedAtUtc = DateTime.UtcNow;

        var pendingRequest = await _dbContext.AppointmentChangeRequests
            .Where(x =>
                x.AppointmentId == appointment.Id &&
                x.Status == AppointmentChangeRequestStatus.Pending)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (pendingRequest is not null)
        {
            pendingRequest.Status = AppointmentChangeRequestStatus.Accepted;
            pendingRequest.RespondedAtUtc = DateTime.UtcNow;
            pendingRequest.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            appointment.Id,
            appointment.Status
        });
    }
    [HttpPost("reject")]
    public async Task<ActionResult<object>> Reject(
    [FromBody] RejectAppointmentRequest request,
    CancellationToken cancellationToken)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            return NotFound("Appointment ne postoji.");

        if (appointment.Status != AppointmentStatus.PendingApproval)
            return BadRequest("Appointment nije u statusu čekanja potvrde.");

        appointment.Status = AppointmentStatus.Rejected;
        appointment.UpdatedAtUtc = DateTime.UtcNow;

        var pendingRequest = await _dbContext.AppointmentChangeRequests
            .Where(x =>
                x.AppointmentId == appointment.Id &&
                x.Status == AppointmentChangeRequestStatus.Pending)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (pendingRequest is not null)
        {
            pendingRequest.Status = AppointmentChangeRequestStatus.Rejected;
            pendingRequest.Reason = request.Reason?.Trim();
            pendingRequest.RespondedAtUtc = DateTime.UtcNow;
            pendingRequest.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            appointment.Id,
            appointment.Status
        });
    }

    [HttpPost("propose-time")]
    public async Task<ActionResult<object>> ProposeTime(
    [FromBody] ProposeAppointmentTimeRequest request,
    CancellationToken cancellationToken)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            return NotFound("Appointment ne postoji.");

        if (appointment.Status != AppointmentStatus.PendingApproval)
            return BadRequest("Predlog novog termina je dozvoljen samo za appointment koji čeka potvrdu.");

        var service = await _dbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == appointment.ServiceId && x.BusinessId == appointment.BusinessId,
                cancellationToken);

        if (service is null)
            return BadRequest("Service ne postoji.");

        var totalDurationMin = await GetTotalServiceDurationAsync(
            service.Id,
            service.EstimatedDurationMin,
            cancellationToken);

        var proposedStart = request.ProposedStartAtUtc;
        var proposedEnd = proposedStart.AddMinutes(totalDurationMin);

        var hasConflict = await _dbContext.Appointments
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id != appointment.Id &&
                x.BusinessId == appointment.BusinessId &&
                x.PrimaryStaffMemberId == appointment.PrimaryStaffMemberId &&
                x.Status != AppointmentStatus.Cancelled &&
                x.Status != AppointmentStatus.Rejected &&
                x.StartAtUtc < proposedEnd &&
                x.EndAtUtc > proposedStart,
                cancellationToken);

        if (hasConflict)
            return BadRequest("Predloženi termin nije dostupan.");

        var existingPendingRequests = await _dbContext.AppointmentChangeRequests
            .Where(x =>
                x.AppointmentId == appointment.Id &&
                x.Status == AppointmentChangeRequestStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var item in existingPendingRequests)
        {
            item.Status = AppointmentChangeRequestStatus.Cancelled;
            item.RespondedAtUtc = DateTime.UtcNow;
            item.UpdatedAtUtc = DateTime.UtcNow;
        }

        var changeRequest = new AppointmentChangeRequest
        {
            AppointmentId = appointment.Id,
            RequestType = AppointmentChangeRequestType.CounterProposal,
            Status = AppointmentChangeRequestStatus.Pending,
            InitiatedBy = ChangeInitiatorType.Business,
            OriginalStartAtUtc = appointment.StartAtUtc,
            OriginalEndAtUtc = appointment.EndAtUtc,
            ProposedStartAtUtc = proposedStart,
            ProposedEndAtUtc = proposedEnd,
            Reason = "Business proposed a different time",
            Message = request.Message?.Trim(),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.AppointmentChangeRequests.Add(changeRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            appointment.Id,
            ChangeRequestId = changeRequest.Id,
            changeRequest.RequestType,
            changeRequest.Status,
            changeRequest.ProposedStartAtUtc,
            changeRequest.ProposedEndAtUtc
        });
    }

    [HttpPost("accept-proposal")]
    public async Task<ActionResult<object>> AcceptProposal(
    [FromBody] AcceptAppointmentProposalRequest request,
    CancellationToken cancellationToken)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            return NotFound("Appointment ne postoji.");

        var changeRequest = await _dbContext.AppointmentChangeRequests
            .FirstOrDefaultAsync(
                x => x.Id == request.ChangeRequestId && x.AppointmentId == request.AppointmentId,
                cancellationToken);

        if (changeRequest is null)
            return NotFound("Change request ne postoji.");

        if (changeRequest.Status != AppointmentChangeRequestStatus.Pending)
            return BadRequest("Change request nije više aktivan.");

        appointment.StartAtUtc = changeRequest.ProposedStartAtUtc;
        appointment.EndAtUtc = changeRequest.ProposedEndAtUtc;
        appointment.Status = AppointmentStatus.Confirmed;
        appointment.UpdatedAtUtc = DateTime.UtcNow;

        changeRequest.Status = AppointmentChangeRequestStatus.Accepted;
        changeRequest.RespondedAtUtc = DateTime.UtcNow;
        changeRequest.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            appointment.Id,
            appointment.Status,
            appointment.StartAtUtc,
            appointment.EndAtUtc,
            ChangeRequestId = changeRequest.Id,
            ChangeRequestStatus = changeRequest.Status
        });
    }

    [HttpPost("reject-proposal")]
    public async Task<ActionResult<object>> RejectProposal(
    [FromBody] RejectAppointmentProposalRequest request,
    CancellationToken cancellationToken)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            return NotFound("Appointment ne postoji.");

        var changeRequest = await _dbContext.AppointmentChangeRequests
            .FirstOrDefaultAsync(
                x => x.Id == request.ChangeRequestId && x.AppointmentId == request.AppointmentId,
                cancellationToken);

        if (changeRequest is null)
            return NotFound("Change request ne postoji.");

        if (changeRequest.Status != AppointmentChangeRequestStatus.Pending)
            return BadRequest("Change request nije više aktivan.");

        changeRequest.Status = AppointmentChangeRequestStatus.Rejected;
        changeRequest.Reason = request.Reason?.Trim();
        changeRequest.RespondedAtUtc = DateTime.UtcNow;
        changeRequest.UpdatedAtUtc = DateTime.UtcNow;

        appointment.Status = AppointmentStatus.Rejected;
        appointment.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            appointment.Id,
            appointment.Status,
            ChangeRequestId = changeRequest.Id,
            ChangeRequestStatus = changeRequest.Status
        });
    }

    [HttpGet("change-requests")]
    public async Task<ActionResult<List<object>>> GetChangeRequests(
    [FromQuery] long appointmentId,
    CancellationToken cancellationToken)
    {
        var items = await _dbContext.AppointmentChangeRequests
            .AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.AppointmentId,
                x.RequestType,
                x.Status,
                x.InitiatedBy,
                x.OriginalStartAtUtc,
                x.OriginalEndAtUtc,
                x.ProposedStartAtUtc,
                x.ProposedEndAtUtc,
                x.Reason,
                x.Message,
                x.CreatedAtUtc,
                x.ExpiresAtUtc,
                x.RespondedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("propose-delay")]
    public async Task<ActionResult<object>> ProposeDelay(
    [FromBody] ProposeDelayRequest request,
    CancellationToken cancellationToken)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            return NotFound("Appointment ne postoji.");

        if (appointment.Status != AppointmentStatus.Confirmed)
            return BadRequest("Delay proposal je dozvoljen samo za potvrđen termin.");

        if (request.DelayMinutes <= 0)
            return BadRequest("DelayMinutes mora biti veći od 0.");

        var proposedStart = appointment.StartAtUtc.AddMinutes(request.DelayMinutes);
        var proposedEnd = appointment.EndAtUtc.AddMinutes(request.DelayMinutes);

        var hasConflict = await _dbContext.Appointments
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id != appointment.Id &&
                x.BusinessId == appointment.BusinessId &&
                x.PrimaryStaffMemberId == appointment.PrimaryStaffMemberId &&
                x.Status != AppointmentStatus.Cancelled &&
                x.Status != AppointmentStatus.Rejected &&
                x.StartAtUtc < proposedEnd &&
                x.EndAtUtc > proposedStart,
                cancellationToken);

        if (hasConflict)
            return BadRequest("Predloženo pomeranje pravi konflikt sa drugim terminom.");

        var existingPendingDelayRequests = await _dbContext.AppointmentChangeRequests
            .Where(x =>
                x.AppointmentId == appointment.Id &&
                x.Status == AppointmentChangeRequestStatus.Pending &&
                x.RequestType == AppointmentChangeRequestType.DelayProposal)
            .ToListAsync(cancellationToken);

        foreach (var item in existingPendingDelayRequests)
        {
            item.Status = AppointmentChangeRequestStatus.Cancelled;
            item.RespondedAtUtc = DateTime.UtcNow;
            item.UpdatedAtUtc = DateTime.UtcNow;
        }

        var now = DateTime.UtcNow;

        var changeRequest = new AppointmentChangeRequest
        {
            AppointmentId = appointment.Id,
            RequestType = AppointmentChangeRequestType.DelayProposal,
            Status = AppointmentChangeRequestStatus.Pending,
            InitiatedBy = ChangeInitiatorType.Business,
            OriginalStartAtUtc = appointment.StartAtUtc,
            OriginalEndAtUtc = appointment.EndAtUtc,
            ProposedStartAtUtc = proposedStart,
            ProposedEndAtUtc = proposedEnd,
            Reason = "Delay proposal",
            Message = request.Message?.Trim(),
            ExpiresAtUtc = now.AddMinutes(15),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.AppointmentChangeRequests.Add(changeRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            appointment.Id,
            ChangeRequestId = changeRequest.Id,
            changeRequest.RequestType,
            changeRequest.Status,
            changeRequest.OriginalStartAtUtc,
            changeRequest.OriginalEndAtUtc,
            changeRequest.ProposedStartAtUtc,
            changeRequest.ProposedEndAtUtc
        });
    }

    [HttpPost("accept-delay")]
    public async Task<ActionResult<object>> AcceptDelay(
    [FromBody] AcceptDelayProposalRequest request,
    CancellationToken cancellationToken)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            return NotFound("Appointment ne postoji.");

        if (appointment.Status != AppointmentStatus.Confirmed)
            return BadRequest("Appointment mora biti confirmed.");

        var changeRequest = await _dbContext.AppointmentChangeRequests
            .FirstOrDefaultAsync(
                x => x.Id == request.ChangeRequestId &&
                     x.AppointmentId == request.AppointmentId &&
                     x.RequestType == AppointmentChangeRequestType.DelayProposal,
                cancellationToken);

        if (changeRequest is null)
            return NotFound("Delay proposal ne postoji.");

        if (changeRequest.Status != AppointmentChangeRequestStatus.Pending)
            return BadRequest("Delay proposal nije više aktivan.");

        appointment.StartAtUtc = changeRequest.ProposedStartAtUtc;
        appointment.EndAtUtc = changeRequest.ProposedEndAtUtc;
        appointment.UpdatedAtUtc = DateTime.UtcNow;

        changeRequest.Status = AppointmentChangeRequestStatus.Accepted;
        changeRequest.RespondedAtUtc = DateTime.UtcNow;
        changeRequest.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            appointment.Id,
            appointment.Status,
            appointment.StartAtUtc,
            appointment.EndAtUtc,
            ChangeRequestId = changeRequest.Id,
            ChangeRequestStatus = changeRequest.Status
        });
    }

    [HttpPost("reject-delay")]
    public async Task<ActionResult<object>> RejectDelay(
    [FromBody] RejectDelayProposalRequest request,
    CancellationToken cancellationToken)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            return NotFound("Appointment ne postoji.");

        if (appointment.Status != AppointmentStatus.Confirmed)
            return BadRequest("Appointment mora biti confirmed.");

        var changeRequest = await _dbContext.AppointmentChangeRequests
            .FirstOrDefaultAsync(
                x => x.Id == request.ChangeRequestId &&
                     x.AppointmentId == request.AppointmentId &&
                     x.RequestType == AppointmentChangeRequestType.DelayProposal,
                cancellationToken);

        if (changeRequest is null)
            return NotFound("Delay proposal ne postoji.");

        if (changeRequest.Status != AppointmentChangeRequestStatus.Pending)
            return BadRequest("Delay proposal nije više aktivan.");

        changeRequest.Status = AppointmentChangeRequestStatus.Rejected;
        changeRequest.Reason = request.Reason?.Trim();
        changeRequest.RespondedAtUtc = DateTime.UtcNow;
        changeRequest.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            appointment.Id,
            appointment.Status,
            appointment.StartAtUtc,
            appointment.EndAtUtc,
            ChangeRequestId = changeRequest.Id,
            ChangeRequestStatus = changeRequest.Status
        });
    }

    [HttpGet("audit-log")]
    public async Task<ActionResult<List<object>>> GetAuditLog(
    [FromQuery] long appointmentId,
    CancellationToken cancellationToken)
    {
        var items = await _dbContext.AppointmentAuditLogs
            .AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.AppointmentId,
                x.ActionType,
                x.Message,
                x.OldValuesJson,
                x.NewValuesJson,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("inbox")]
    public async Task<ActionResult<List<AppointmentInboxItemDto>>> GetInbox(
        [FromQuery] long businessId,
        CancellationToken cancellationToken)
    {
        var appointments = await _dbContext.Appointments
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                (x.Status == AppointmentStatus.PendingApproval || x.Status == AppointmentStatus.Confirmed))
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync(cancellationToken);

        var appointmentIds = appointments.Select(x => x.Id).ToList();

        var pendingChangeRequests = await _dbContext.AppointmentChangeRequests
            .AsNoTracking()
            .Where(x =>
                appointmentIds.Contains(x.AppointmentId) &&
                x.Status == AppointmentChangeRequestStatus.Pending)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var services = await _dbContext.Services
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var staff = await _dbContext.StaffMembers
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var latestPendingByAppointment = pendingChangeRequests
            .GroupBy(x => x.AppointmentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.CreatedAtUtc).First());

        var result = new List<AppointmentInboxItemDto>();

        foreach (var appointment in appointments)
        {
            latestPendingByAppointment.TryGetValue(appointment.Id, out var pendingChange);

            var shouldInclude =
                appointment.Status == AppointmentStatus.PendingApproval ||
                pendingChange is not null;

            if (!shouldInclude)
                continue;

            services.TryGetValue(appointment.ServiceId, out var serviceName);

            string? staffDisplayName = null;
            if (appointment.PrimaryStaffMemberId.HasValue &&
                staff.TryGetValue(appointment.PrimaryStaffMemberId.Value, out var staffName))
            {
                staffDisplayName = staffName;
            }

            result.Add(new AppointmentInboxItemDto
            {
                AppointmentId = appointment.Id,
                ChangeRequestId = pendingChange?.Id,
                CustomerName = appointment.CustomerName,
                CustomerPhone = appointment.CustomerPhone,
                ServiceId = appointment.ServiceId,
                ServiceName = serviceName ?? string.Empty,
                StaffMemberId = appointment.PrimaryStaffMemberId,
                StaffDisplayName = staffDisplayName,
                AppointmentStatus = appointment.Status.ToString(),
                ChangeRequestType = pendingChange?.RequestType.ToString(),
                ChangeRequestStatus = pendingChange?.Status.ToString(),
                StartAtUtc = appointment.StartAtUtc,
                EndAtUtc = appointment.EndAtUtc,
                ProposedStartAtUtc = pendingChange?.ProposedStartAtUtc,
                ProposedEndAtUtc = pendingChange?.ProposedEndAtUtc,
                Message = pendingChange?.Message,
                ExpiresAtUtc = pendingChange?.ExpiresAtUtc
            });
        }

        return Ok(result
            .OrderBy(x => x.ExpiresAtUtc ?? DateTime.MaxValue)
            .ThenBy(x => x.StartAtUtc)
            .ToList());
    }

    private async Task AddAuditLogAsync(
    long appointmentId,
    string actionType,
    string? message,
    string? oldValuesJson,
    string? newValuesJson,
    CancellationToken cancellationToken)
    {
        var log = new AppointmentAuditLog
        {
            AppointmentId = appointmentId,
            ActionType = actionType,
            Message = message,
            OldValuesJson = oldValuesJson,
            NewValuesJson = newValuesJson,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.AppointmentAuditLogs.Add(log);
        await _dbContext.SaveChangesAsync(cancellationToken);
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