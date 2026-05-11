using BookingPlatform.Domain.Appointments;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Services;

public sealed class AppointmentWorkflowService : IAppointmentWorkflowService
{
    private readonly BookingDbContext _dbContext;
    private readonly IChatSystemMessageService _chatSystemMessageService;

    public AppointmentWorkflowService(
        BookingDbContext dbContext,
        IChatSystemMessageService chatSystemMessageService)
    {
        _dbContext = dbContext;
        _chatSystemMessageService = chatSystemMessageService;
    }

    public async Task<UpdateConfirmedAppointmentResult> CompleteConfirmedAsync(
        long appointmentId,
        string? note,
        CancellationToken cancellationToken)
    {
        return await UpdateConfirmedAppointmentStatusAsync(
            appointmentId,
            AppointmentStatus.Completed,
            "Completed",
            "Termin je označen kao završen.",
            "Pending promene su zatvorene jer je termin označen kao završen.",
            note,
            cancellationToken);
    }

    public async Task<UpdateConfirmedAppointmentResult> MarkNoShowAsync(
        long appointmentId,
        string? note,
        CancellationToken cancellationToken)
    {
        return await UpdateConfirmedAppointmentStatusAsync(
            appointmentId,
            AppointmentStatus.NoShow,
            "NoShow",
            "Termin je označen kao nedolazak klijenta.",
            "Pending promene su zatvorene jer je termin označen kao nedolazak.",
            note,
            cancellationToken);
    }

    public async Task<UpdateConfirmedAppointmentResult> CancelConfirmedAsync(
        long appointmentId,
        string? note,
        CancellationToken cancellationToken)
    {
        return await UpdateConfirmedAppointmentStatusAsync(
            appointmentId,
            AppointmentStatus.Cancelled,
            "Cancelled",
            "Termin je otkazan.",
            "Pending promene su zatvorene jer je termin otkazan.",
            note,
            cancellationToken);
    }

    public async Task<RescheduleDecisionResult> AcceptRescheduleRequestAsync(
        long appointmentId,
        long changeRequestId,
        CancellationToken cancellationToken)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == appointmentId, cancellationToken);

        if (appointment is null)
        {
            return new RescheduleDecisionResult
            {
                NotFound = true,
                ErrorMessage = "Termin ne postoji.",
                ErrorReasonCode = "appointment_not_found"
            };
        }

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            return new RescheduleDecisionResult
            {
                ErrorMessage = "Zahtev za promenu termina može da se obradi samo za potvrđen termin.",
                ErrorReasonCode = "appointment_not_confirmed"
            };
        }

        var changeRequest = await _dbContext.AppointmentChangeRequests
            .FirstOrDefaultAsync(
                x => x.Id == changeRequestId &&
                     x.AppointmentId == appointmentId &&
                     x.RequestType == AppointmentChangeRequestType.RescheduleRequest,
                cancellationToken);

        if (changeRequest is null)
        {
            return new RescheduleDecisionResult
            {
                NotFound = true,
                ErrorMessage = "Zahtev za promenu termina ne postoji.",
                ErrorReasonCode = "reschedule_request_not_found"
            };
        }

        if (changeRequest.Status != AppointmentChangeRequestStatus.Pending)
        {
            return new RescheduleDecisionResult
            {
                ErrorMessage = GetInactiveChangeRequestMessage(changeRequest, "Zahtev za promenu termina"),
                ErrorReasonCode = GetChangeRequestReasonCode(changeRequest, "reschedule_request")
            };
        }

        var now = DateTime.UtcNow;

        appointment.StartAtUtc = changeRequest.ProposedStartAtUtc;
        appointment.EndAtUtc = changeRequest.ProposedEndAtUtc;

        if (changeRequest.ProposedStaffMemberId.HasValue)
        {
            appointment.PrimaryStaffMemberId = changeRequest.ProposedStaffMemberId.Value;
        }

        appointment.UpdatedAtUtc = now;

        var otherPendingRequests = await _dbContext.AppointmentChangeRequests
            .Where(x =>
                x.AppointmentId == appointment.Id &&
                x.Status == AppointmentChangeRequestStatus.Pending &&
                x.Id != changeRequest.Id)
            .ToListAsync(cancellationToken);

        foreach (var item in otherPendingRequests)
        {
            item.Status = AppointmentChangeRequestStatus.Cancelled;
            item.RespondedAtUtc = now;
            item.UpdatedAtUtc = now;
        }

        changeRequest.Status = AppointmentChangeRequestStatus.Accepted;
        changeRequest.RespondedAtUtc = now;
        changeRequest.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RescheduleDecisionResult
        {
            Succeeded = true,
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status,
            ChangeRequestId = changeRequest.Id,
            ChangeRequestStatus = changeRequest.Status,
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            Message = "Zahtev za promenu termina je prihvaćen.",
            OriginalStartAtUtc = changeRequest.OriginalStartAtUtc,
            OriginalEndAtUtc = changeRequest.OriginalEndAtUtc,
            ProposedStartAtUtc = changeRequest.ProposedStartAtUtc,
            ProposedEndAtUtc = changeRequest.ProposedEndAtUtc,
            ProposedStaffMemberId = changeRequest.ProposedStaffMemberId,
            Reason = changeRequest.Reason
        };
    }

    public async Task<RescheduleDecisionResult> RejectRescheduleRequestAsync(
        long appointmentId,
        long changeRequestId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == appointmentId, cancellationToken);

        if (appointment is null)
        {
            return new RescheduleDecisionResult
            {
                NotFound = true,
                ErrorMessage = "Termin ne postoji.",
                ErrorReasonCode = "appointment_not_found"
            };
        }

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            return new RescheduleDecisionResult
            {
                ErrorMessage = "Zahtev za promenu termina može da se obradi samo za potvrđen termin.",
                ErrorReasonCode = "appointment_not_confirmed"
            };
        }

        var changeRequest = await _dbContext.AppointmentChangeRequests
            .FirstOrDefaultAsync(
                x => x.Id == changeRequestId &&
                     x.AppointmentId == appointmentId &&
                     x.RequestType == AppointmentChangeRequestType.RescheduleRequest,
                cancellationToken);

        if (changeRequest is null)
        {
            return new RescheduleDecisionResult
            {
                NotFound = true,
                ErrorMessage = "Zahtev za promenu termina ne postoji.",
                ErrorReasonCode = "reschedule_request_not_found"
            };
        }

        if (changeRequest.Status != AppointmentChangeRequestStatus.Pending)
        {
            return new RescheduleDecisionResult
            {
                ErrorMessage = GetInactiveChangeRequestMessage(changeRequest, "Zahtev za promenu termina"),
                ErrorReasonCode = GetChangeRequestReasonCode(changeRequest, "reschedule_request")
            };
        }

        changeRequest.Status = AppointmentChangeRequestStatus.Rejected;
        changeRequest.Reason = reason?.Trim();
        changeRequest.RespondedAtUtc = DateTime.UtcNow;
        changeRequest.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RescheduleDecisionResult
        {
            Succeeded = true,
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status,
            ChangeRequestId = changeRequest.Id,
            ChangeRequestStatus = changeRequest.Status,
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            Message = "Zahtev za promenu termina je odbijen.",
            OriginalStartAtUtc = changeRequest.OriginalStartAtUtc,
            OriginalEndAtUtc = changeRequest.OriginalEndAtUtc,
            ProposedStartAtUtc = changeRequest.ProposedStartAtUtc,
            ProposedEndAtUtc = changeRequest.ProposedEndAtUtc,
            ProposedStaffMemberId = changeRequest.ProposedStaffMemberId,
            Reason = changeRequest.Reason
        };
    }

    public async Task<DelayProposalResult> ProposeDelayAsync(
        long appointmentId,
        int delayMinutes,
        string? message,
        CancellationToken cancellationToken)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == appointmentId, cancellationToken);

        if (appointment is null)
        {
            return new DelayProposalResult
            {
                NotFound = true,
                ErrorMessage = "Termin ne postoji.",
                ErrorReasonCode = "appointment_not_found"
            };
        }

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            return new DelayProposalResult
            {
                ErrorMessage = "Pomeranje može da se predloži samo za potvrđen termin.",
                ErrorReasonCode = "appointment_not_confirmed"
            };
        }

        if (delayMinutes <= 0)
        {
            return new DelayProposalResult
            {
                ErrorMessage = "Pomeranje mora biti veće od 0 minuta.",
                ErrorReasonCode = "invalid_delay_minutes"
            };
        }

        var proposedStart = appointment.StartAtUtc.AddMinutes(delayMinutes);
        var proposedEnd = appointment.EndAtUtc.AddMinutes(delayMinutes);

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
        {
            return new DelayProposalResult
            {
                ErrorMessage = "Predloženo pomeranje upada u drugi zauzet termin.",
                ErrorReasonCode = "appointment_conflict",
                HasAppointmentConflict = true
            };
        }

        var existingPendingDelayRequests = await _dbContext.AppointmentChangeRequests
            .Where(x =>
                x.AppointmentId == appointment.Id &&
                x.Status == AppointmentChangeRequestStatus.Pending &&
                x.RequestType == AppointmentChangeRequestType.DelayProposal)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var item in existingPendingDelayRequests)
        {
            item.Status = AppointmentChangeRequestStatus.Cancelled;
            item.RespondedAtUtc = now;
            item.UpdatedAtUtc = now;
        }

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
            Message = message?.Trim(),
            ExpiresAtUtc = now.AddMinutes(15),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.AppointmentChangeRequests.Add(changeRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _chatSystemMessageService.SendDelayProposalToCustomerAsync(
            appointment,
            changeRequest,
            cancellationToken);

        return new DelayProposalResult
        {
            Succeeded = true,
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status,
            ChangeRequestId = changeRequest.Id,
            ChangeRequestStatus = changeRequest.Status,
            StartAtUtc = changeRequest.ProposedStartAtUtc,
            EndAtUtc = changeRequest.ProposedEndAtUtc,
            Message = "Predloženo je pomeranje termina."
        };
    }

    public async Task<ProposeTimeResult> ProposeTimeAsync(
        long appointmentId,
        DateTime proposedStartAtUtc,
        int? finalDurationMin,
        string? message,
        long? proposedStaffMemberId,
        CancellationToken cancellationToken)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == appointmentId, cancellationToken);

        if (appointment is null)
        {
            return new ProposeTimeResult
            {
                NotFound = true,
                ErrorMessage = "Termin ne postoji.",
                ErrorReasonCode = "appointment_not_found"
            };
        }

        var latestPendingChangeRequest = await _dbContext.AppointmentChangeRequests
            .Where(x =>
                x.AppointmentId == appointment.Id &&
                x.Status == AppointmentChangeRequestStatus.Pending)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var isPendingApprovalFlow = appointment.Status == AppointmentStatus.PendingApproval;
        var isConfirmedFlow = appointment.Status == AppointmentStatus.Confirmed;

        var isConfirmedRescheduleFlow =
            isConfirmedFlow &&
            latestPendingChangeRequest is not null &&
            latestPendingChangeRequest.RequestType == AppointmentChangeRequestType.RescheduleRequest &&
            latestPendingChangeRequest.Status == AppointmentChangeRequestStatus.Pending;

        if (!isPendingApprovalFlow && !isConfirmedFlow)
        {
            return new ProposeTimeResult
            {
                ErrorMessage = "Novi termin može da se predloži samo za termin koji čeka potvrdu ili za već potvrđen termin.",
                ErrorReasonCode = "appointment_not_eligible_for_propose_time"
            };
        }

        var service = await _dbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == appointment.ServiceId && x.BusinessId == appointment.BusinessId,
                cancellationToken);

        if (service is null)
        {
            return new ProposeTimeResult
            {
                ErrorMessage = "Izabrana usluga ne postoji.",
                ErrorReasonCode = "service_not_found"
            };
        }

        var stepDurations = await _dbContext.ServiceSteps
            .AsNoTracking()
            .Where(x => x.ServiceId == service.Id)
            .Select(x => x.DurationMin)
            .ToListAsync(cancellationToken);

        var defaultDurationMin = stepDurations.Count == 0
            ? service.EstimatedDurationMin
            : stepDurations.Sum();

        if (finalDurationMin.HasValue)
        {
            if (finalDurationMin.Value <= 0)
            {
                return new ProposeTimeResult
                {
                    ErrorMessage = "Trajanje termina mora biti veće od 0 minuta.",
                    ErrorReasonCode = "invalid_final_duration"
                };
            }

            if (finalDurationMin.Value % 5 != 0)
            {
                return new ProposeTimeResult
                {
                    ErrorMessage = "Trajanje termina mora biti zadato u koracima od 5 minuta.",
                    ErrorReasonCode = "invalid_duration_step"
                };
            }
        }

        if (!appointment.PrimaryStaffMemberId.HasValue &&
            !proposedStaffMemberId.HasValue)
        {
            return new ProposeTimeResult
            {
                ErrorMessage = "Za predlog novog termina je potrebno da termin ima izabranog zaposlenog.",
                ErrorReasonCode = "staff_required"
            };
        }

        var effectiveProposedStaffMemberId =
            proposedStaffMemberId ?? appointment.PrimaryStaffMemberId;

        var effectiveDurationMin = finalDurationMin ?? defaultDurationMin;
        var proposedEnd = proposedStartAtUtc.AddMinutes(effectiveDurationMin);

        var existingPendingRequests = await _dbContext.AppointmentChangeRequests
            .Where(x =>
                x.AppointmentId == appointment.Id &&
                x.Status == AppointmentChangeRequestStatus.Pending)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var item in existingPendingRequests)
        {
            if (isConfirmedRescheduleFlow &&
                latestPendingChangeRequest is not null &&
                item.Id == latestPendingChangeRequest.Id)
            {
                item.Status = AppointmentChangeRequestStatus.Rejected;
                item.Reason = "Owner sent a counter proposal";
                item.RespondedAtUtc = now;
                item.UpdatedAtUtc = now;
                continue;
            }

            item.Status = AppointmentChangeRequestStatus.Cancelled;
            item.RespondedAtUtc = now;
            item.UpdatedAtUtc = now;
        }

        var changeRequest = new AppointmentChangeRequest
        {
            AppointmentId = appointment.Id,
            RequestType = AppointmentChangeRequestType.CounterProposal,
            Status = AppointmentChangeRequestStatus.Pending,
            InitiatedBy = ChangeInitiatorType.Business,
            OriginalStartAtUtc = appointment.StartAtUtc,
            OriginalEndAtUtc = appointment.EndAtUtc,
            ProposedStartAtUtc = proposedStartAtUtc,
            ProposedEndAtUtc = proposedEnd,
            ProposedStaffMemberId = effectiveProposedStaffMemberId,
            Reason = isConfirmedRescheduleFlow
                ? "Business sent a counter proposal for customer reschedule request"
                : "Business proposed a different time",
            Message = message?.Trim(),
            ExpiresAtUtc = now.AddHours(12),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.AppointmentChangeRequests.Add(changeRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _chatSystemMessageService.SendTimeProposalToCustomerAsync(
            appointment,
            changeRequest,
            cancellationToken);

        return new ProposeTimeResult
        {
            Succeeded = true,
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status,
            ChangeRequestId = changeRequest.Id,
            ChangeRequestStatus = changeRequest.Status,
            StartAtUtc = changeRequest.ProposedStartAtUtc,
            EndAtUtc = changeRequest.ProposedEndAtUtc,
            ProposedStaffMemberId = changeRequest.ProposedStaffMemberId,
            EffectiveDurationMin = effectiveDurationMin,
            Message = finalDurationMin.HasValue
                ? $"Predložen je novi termin sa trajanjem od {finalDurationMin.Value} minuta."
                : "Predložen je novi termin.",
            IsConfirmedRescheduleFlow = isConfirmedRescheduleFlow
        };
    }

    private async Task<UpdateConfirmedAppointmentResult> UpdateConfirmedAppointmentStatusAsync(
        long appointmentId,
        AppointmentStatus targetStatus,
        string action,
        string successMessage,
        string cancelPendingReason,
        string? note,
        CancellationToken cancellationToken)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == appointmentId, cancellationToken);

        if (appointment is null)
        {
            return new UpdateConfirmedAppointmentResult
            {
                NotFound = true,
                ErrorMessage = "Termin ne postoji.",
                ErrorReasonCode = "appointment_not_found"
            };
        }

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            return new UpdateConfirmedAppointmentResult
            {
                ErrorMessage = targetStatus switch
                {
                    AppointmentStatus.Completed => "Termin može da se označi kao završen samo ako je potvrđen.",
                    AppointmentStatus.NoShow => "Termin može da se označi kao nedolazak samo ako je potvrđen.",
                    AppointmentStatus.Cancelled => "Termin može da se otkaže samo ako je potvrđen.",
                    _ => "Termin nije u ispravnom statusu za ovu radnju."
                },
                ErrorReasonCode = "appointment_not_confirmed"
            };
        }

        var now = DateTime.UtcNow;

        appointment.Status = targetStatus;
        appointment.UpdatedAtUtc = now;

        var pendingRequests = await _dbContext.AppointmentChangeRequests
            .Where(x =>
                x.AppointmentId == appointment.Id &&
                x.Status == AppointmentChangeRequestStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var pendingRequest in pendingRequests)
        {
            pendingRequest.Status = AppointmentChangeRequestStatus.Cancelled;
            pendingRequest.Reason = cancelPendingReason;
            pendingRequest.RespondedAtUtc = now;
            pendingRequest.UpdatedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UpdateConfirmedAppointmentResult
        {
            Succeeded = true,
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status,
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            Action = action,
            Message = successMessage,
            Note = note?.Trim()
        };
    }

    private static string GetChangeRequestReasonCode(
        AppointmentChangeRequest changeRequest,
        string baseCode)
    {
        return changeRequest.Status switch
        {
            AppointmentChangeRequestStatus.Expired => $"{baseCode}_expired",
            AppointmentChangeRequestStatus.Accepted => $"{baseCode}_accepted",
            AppointmentChangeRequestStatus.Rejected => $"{baseCode}_rejected",
            AppointmentChangeRequestStatus.Cancelled => $"{baseCode}_cancelled",
            AppointmentChangeRequestStatus.Pending => $"{baseCode}_not_pending",
            _ => $"{baseCode}_inactive"
        };
    }

    private static string GetInactiveChangeRequestMessage(
        AppointmentChangeRequest changeRequest,
        string entityDisplayName)
    {
        return changeRequest.Status switch
        {
            AppointmentChangeRequestStatus.Expired => $"{entityDisplayName} više nije aktivan jer je istekao.",
            AppointmentChangeRequestStatus.Accepted => $"{entityDisplayName} je već prihvaćen.",
            AppointmentChangeRequestStatus.Rejected => $"{entityDisplayName} je već odbijen.",
            AppointmentChangeRequestStatus.Cancelled => $"{entityDisplayName} više nije aktivan jer je otkazan.",
            AppointmentChangeRequestStatus.Pending => $"{entityDisplayName} je i dalje aktivan.",
            _ => $"{entityDisplayName} više nije aktivan."
        };
    }
}