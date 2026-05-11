using BookingPlatform.Api.Helpers;
using BookingPlatform.Api.Services;
using BookingPlatform.Contracts.Appointments;
using BookingPlatform.Domain.Appointments;
using BookingPlatform.Domain.Services;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/Appointments")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly BookingDbContext _dbContext;
    private readonly IAppointmentSchedulingService _appointmentSchedulingService;
    private readonly IChatSystemMessageService _chatSystemMessageService;

    public AppointmentsController(
        BookingDbContext dbContext,
        IAppointmentSchedulingService appointmentSchedulingService,
        IChatSystemMessageService chatSystemMessageService)
    {
        _dbContext = dbContext;
        _appointmentSchedulingService = appointmentSchedulingService;
        _chatSystemMessageService = chatSystemMessageService;
    }



    [HttpPost]
    [ProducesResponseType(typeof(CreateAppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(CreateAppointmentErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateAppointmentResponse>> Create(
    [FromBody] CreateAppointmentRequest request,
    CancellationToken cancellationToken)
    {
        var service = await _dbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.ServiceId && x.BusinessId == request.BusinessId,
                cancellationToken);

        if (service is null)
        {
            return BadRequest(BuildCreateAppointmentErrorResponse(
                "Izabrana usluga ne postoji.",
                "service_not_found"));
        }

        if (request.PrimaryStaffMemberId.HasValue)
        {
            var staffServiceValidationError = await StaffServiceValidationHelper.ValidateStaffCanPerformServiceAsync(
                _dbContext,
                request.BusinessId,
                request.ServiceId,
                request.PrimaryStaffMemberId,
                cancellationToken);

            if (staffServiceValidationError is not null)
            {
                return BadRequest(BuildCreateAppointmentErrorResponse(
                    staffServiceValidationError,
                    MapStaffServiceValidationReasonCode(staffServiceValidationError)));
            }

            var staffResourceValidationError = await StaffResourceValidationHelper.ValidateStaffCanUseResourceAsync(
                _dbContext,
                request.BusinessId,
                request.PrimaryStaffMemberId,
                request.ResourceId,
                cancellationToken);

            if (staffResourceValidationError is not null)
            {
                return BadRequest(BuildCreateAppointmentErrorResponse(
                    staffResourceValidationError,
                    MapStaffResourceValidationReasonCode(staffResourceValidationError)));
            }
        }

        if (!request.PrimaryStaffMemberId.HasValue)
        {
            return BadRequest(BuildCreateAppointmentErrorResponse(
                "Potrebno je izabrati zaposlenog.",
                "staff_required"));
        }

        var resourceValidationError = await ValidateServiceResourceSelectionAsync(
            request.BusinessId,
            request.ServiceId,
            request.ResourceId,
            cancellationToken);

        if (resourceValidationError is not null)
        {
            return BadRequest(BuildCreateAppointmentErrorResponse(
                resourceValidationError,
                MapCreateResourceSelectionReasonCode(resourceValidationError)));
        }

        var totalDurationMin = await _appointmentSchedulingService.GetTotalServiceDurationAsync(
            service.Id,
            service.EstimatedDurationMin,
            cancellationToken);

        var proposedStart = request.StartAtUtc;
        var proposedEnd = request.StartAtUtc.AddMinutes(totalDurationMin);

        var availability = await CheckAvailabilityViaServiceAsync(
    request.BusinessId,
    request.ServiceId,
    request.PrimaryStaffMemberId.Value,
    request.ResourceId,
    proposedStart,
    proposedEnd,
    null,
    false,
    false,
    false,
    cancellationToken);

        if (!availability.IsAvailable)
            return BadRequest(BuildCreateAppointmentErrorResponse(availability));

        var isAlignedToSlotGrid = await _appointmentSchedulingService.IsStartAlignedToBusinessSlotGridAsync(
            request.BusinessId,
            request.PrimaryStaffMemberId.Value,
            proposedStart,
            cancellationToken);

        // Normalni slotovi prolaze.
        // Međutermini koje je sistem ponudio prolaze zato što je availability već potvrdio da ima mesta.
        if (!isAlignedToSlotGrid)
        {
            availability.HasSlotGridViolation = true;
            availability.ReasonCodes.Add("outside_slot_grid_but_available");
        }
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
        {
            return Unauthorized("Korisnik nije autentifikovan.");
        }

        var customerProfile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.AppUserId == userId.Value,
                cancellationToken);

        if (customerProfile is null)
        {
            return BadRequest(BuildCreateAppointmentErrorResponse(
                "Korisnik nema povezan klijent profil.",
                "customer_profile_not_found"));
        }

        var businessCustomer = await _dbContext.BusinessCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.BusinessId == request.BusinessId &&
                     x.CustomerProfileId == customerProfile.Id &&
                     x.IsActive,
                cancellationToken);

        if (businessCustomer is null)
        {
            return BadRequest(BuildCreateAppointmentErrorResponse(
                "Klijent nije povezan sa izabranim biznisom.",
                "business_customer_not_found"));
        }

        var customerName = string.IsNullOrWhiteSpace(businessCustomer.FullName)
            ? request.CustomerName?.Trim()
            : businessCustomer.FullName.Trim();

        var customerPhone = string.IsNullOrWhiteSpace(businessCustomer.Phone)
            ? request.CustomerPhone?.Trim()
            : businessCustomer.Phone.Trim();

        if (string.IsNullOrWhiteSpace(customerName))
        {
            return BadRequest(BuildCreateAppointmentErrorResponse(
                "Ime klijenta je obavezno.",
                "customer_name_required"));
        }

        if (string.IsNullOrWhiteSpace(customerPhone))
        {
            customerPhone = "";
        }
        var now = DateTime.UtcNow;

        var appointment = new Appointment
        {
            BusinessId = request.BusinessId,
            ServiceId = request.ServiceId,
            PrimaryStaffMemberId = request.PrimaryStaffMemberId,
            ResourceId = request.ResourceId,
            BusinessCustomerId = businessCustomer.Id,
            CustomerName = customerName,
            CustomerPhone = customerPhone,
            StartAtUtc = proposedStart,
            EndAtUtc = proposedEnd,
            Status = AppointmentStatus.PendingApproval,
            Notes = request.Notes?.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.Appointments.Add(appointment);
        await _dbContext.SaveChangesAsync(cancellationToken);



        await _appointmentSchedulingService.CreateAppointmentStaffUsagesAsync(
            appointment.Id,
            appointment.BusinessId,
            appointment.ServiceId,
            appointment.PrimaryStaffMemberId!.Value,
            appointment.StartAtUtc,
            appointment.EndAtUtc,
            cancellationToken);

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
            ExpiresAtUtc = GetNewBookingRequestExpirationUtc(now),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.AppointmentChangeRequests.Add(changeRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (appointment.PrimaryStaffMemberId.HasValue)
        {
            await _appointmentSchedulingService.CreateAppointmentStaffUsagesAsync(
                appointment.Id,
                appointment.BusinessId,
                appointment.ServiceId,
                appointment.PrimaryStaffMemberId.Value,
                appointment.StartAtUtc,
                appointment.EndAtUtc,
                cancellationToken);
        }

        await _chatSystemMessageService.SendCustomerRequestedNewBookingToBusinessAsync(
    appointment,
    changeRequest,
    cancellationToken);

        return Ok(new CreateAppointmentResponse
        {
            Id = appointment.Id,
            BusinessId = appointment.BusinessId,
            ServiceId = appointment.ServiceId,
            PrimaryStaffMemberId = appointment.PrimaryStaffMemberId,
            ResourceId = appointment.ResourceId,
            CustomerName = appointment.CustomerName,
            CustomerPhone = appointment.CustomerPhone,
            Status = appointment.Status.ToString(),
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            ChangeRequestId = changeRequest.Id,
            ChangeRequestStatus = changeRequest.Status.ToString(),
            Message = "Zahtev za termin je uspešno poslat."
        });
    }



    [HttpPost("accept-proposal")]
    [ProducesResponseType(typeof(AppointmentChangeActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentChangeActionResponse>> AcceptProposal(
    [FromBody] AcceptAppointmentProposalRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        var changeRequest = await _dbContext.AppointmentChangeRequests
            .FirstOrDefaultAsync(
                x => x.Id == request.ChangeRequestId && x.AppointmentId == request.AppointmentId,
                cancellationToken);

        if (changeRequest is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Predlog promene ne postoji.",
                "counter_proposal_not_found"));
        }

        if (changeRequest.Status != AppointmentChangeRequestStatus.Pending)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                GetInactiveChangeRequestMessage(changeRequest, "Predlog termina"),
                GetChangeRequestReasonCode(changeRequest, "counter_proposal")));
        }

        var scheduleValidationError = await ValidateAcceptedScheduleAsync(
            appointment,
            changeRequest.ProposedStartAtUtc,
            changeRequest.ProposedEndAtUtc,
            cancellationToken);

        if (scheduleValidationError is not null)
            return BadRequest(BuildAppointmentOperationErrorResponse(scheduleValidationError));

        appointment.StartAtUtc = changeRequest.ProposedStartAtUtc;
        appointment.EndAtUtc = changeRequest.ProposedEndAtUtc;
        appointment.Status = AppointmentStatus.Confirmed;
        appointment.UpdatedAtUtc = DateTime.UtcNow;

        changeRequest.Status = AppointmentChangeRequestStatus.Accepted;
        changeRequest.RespondedAtUtc = DateTime.UtcNow;
        changeRequest.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (appointment.PrimaryStaffMemberId.HasValue)
        {
            await _appointmentSchedulingService.CreateAppointmentStaffUsagesAsync(
                appointment.Id,
                appointment.BusinessId,
                appointment.ServiceId,
                appointment.PrimaryStaffMemberId.Value,
                appointment.StartAtUtc,
                appointment.EndAtUtc,
                cancellationToken);
        }

        await AddAuditLogAsync(
                    appointment.Id,
            "CounterProposalAccepted",
            "Klijent je prihvatio novi termin.",
            null,
            $"Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o};Status={appointment.Status}",
            cancellationToken);

        await _chatSystemMessageService.SendCustomerAcceptedProposalToBusinessAsync(
    appointment,
    changeRequest,
    cancellationToken);



        return Ok(new AppointmentChangeActionResponse
        {
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status.ToString(),
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            DurationMin = (int)(appointment.EndAtUtc - appointment.StartAtUtc).TotalMinutes,
            ChangeRequestId = changeRequest.Id,
            ChangeRequestStatus = changeRequest.Status.ToString(),
            Message = "Novi termin je prihvaćen."
        });
    }

    [HttpPost("reject-proposal")]
    [ProducesResponseType(typeof(AppointmentChangeActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentChangeActionResponse>> RejectProposal(
    [FromBody] RejectAppointmentProposalRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        var changeRequest = await _dbContext.AppointmentChangeRequests
            .FirstOrDefaultAsync(
                x => x.Id == request.ChangeRequestId && x.AppointmentId == request.AppointmentId,
                cancellationToken);

        if (changeRequest is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Predlog promene ne postoji.",
                "counter_proposal_not_found"));
        }

        if (changeRequest.Status != AppointmentChangeRequestStatus.Pending)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                GetInactiveChangeRequestMessage(changeRequest, "Predlog termina"),
                GetChangeRequestReasonCode(changeRequest, "counter_proposal")));
        }

        changeRequest.Status = AppointmentChangeRequestStatus.Rejected;
        changeRequest.Reason = request.Reason?.Trim();
        changeRequest.RespondedAtUtc = DateTime.UtcNow;
        changeRequest.UpdatedAtUtc = DateTime.UtcNow;

        appointment.Status = AppointmentStatus.Rejected;
        appointment.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (appointment.PrimaryStaffMemberId.HasValue)
        {
            await _appointmentSchedulingService.CreateAppointmentStaffUsagesAsync(
                appointment.Id,
                appointment.BusinessId,
                appointment.ServiceId,
                appointment.PrimaryStaffMemberId.Value,
                appointment.StartAtUtc,
                appointment.EndAtUtc,
                cancellationToken);
        }

        await AddAuditLogAsync(
                    appointment.Id,
            "CounterProposalRejected",
            request.Reason?.Trim() ?? "Klijent je odbio novi termin.",
            null,
            $"Status={appointment.Status}",
            cancellationToken);

        await _chatSystemMessageService.SendCustomerRejectedProposalToBusinessAsync(
    appointment,
    changeRequest,
    cancellationToken);

        return Ok(new AppointmentChangeActionResponse
        {
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status.ToString(),
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            DurationMin = (int)(appointment.EndAtUtc - appointment.StartAtUtc).TotalMinutes,
            ChangeRequestId = changeRequest.Id,
            ChangeRequestStatus = changeRequest.Status.ToString(),
            Message = "Novi termin je odbijen."
        });
    }

    [HttpPost("cancel-confirmed")]
    [ProducesResponseType(typeof(AppointmentActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentActionResponse>> CancelConfirmed(
    [FromBody] CancelConfirmedAppointmentRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                "Samo potvrđen termin može da se otkaže.",
                "appointment_not_confirmed"));
        }

        var now = DateTime.UtcNow;

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.UpdatedAtUtc = now;

        var pendingRequests = await _dbContext.AppointmentChangeRequests
            .Where(x =>
                x.AppointmentId == appointment.Id &&
                x.Status == AppointmentChangeRequestStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var pendingRequest in pendingRequests)
        {
            pendingRequest.Status = AppointmentChangeRequestStatus.Cancelled;
            pendingRequest.Reason = request.Reason?.Trim();
            pendingRequest.RespondedAtUtc = now;
            pendingRequest.UpdatedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await AddAuditLogAsync(
            appointment.Id,
            "CancelledByCustomer",
            string.IsNullOrWhiteSpace(request.Reason)
                ? "Klijent je otkazao potvrđen termin."
                : $"Klijent je otkazao potvrđen termin. Razlog: {request.Reason!.Trim()}",
            $"Status={AppointmentStatus.Confirmed};Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o}",
            $"Status={appointment.Status};Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o}",
            cancellationToken);

        await _chatSystemMessageService.SendCustomerCancelledAppointmentToBusinessAsync(
    appointment,
    request.Reason,
    cancellationToken);

        return Ok(new AppointmentActionResponse
        {
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status.ToString(),
            Action = "CancelledByCustomer",
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            Message = "Termin je uspešno otkazan."
        });
    }

    [HttpPost("withdraw-request")]
    [ProducesResponseType(typeof(AppointmentActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentActionResponse>> WithdrawRequest(
    [FromBody] WithdrawAppointmentRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        if (appointment.Status != AppointmentStatus.PendingApproval)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                "Samo zahtev koji čeka odobrenje može da se povuče.",
                "appointment_not_pending_approval"));
        }

        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized("Korisnik nije autentifikovan.");

        var customerProfile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.AppUserId == userId.Value,
                cancellationToken);

        if (customerProfile is null)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                "Korisnik nema povezan klijent profil.",
                "customer_profile_not_found"));
        }

        var hasCustomerAccess = await _dbContext.BusinessCustomers
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == appointment.BusinessCustomerId &&
                     x.BusinessId == appointment.BusinessId &&
                     x.CustomerProfileId == customerProfile.Id &&
                     x.IsActive,
                cancellationToken);

        if (!hasCustomerAccess)
            return Forbid();

        var now = DateTime.UtcNow;

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.UpdatedAtUtc = now;

        var pendingRequests = await _dbContext.AppointmentChangeRequests
            .Where(x =>
                x.AppointmentId == appointment.Id &&
                x.Status == AppointmentChangeRequestStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var pendingRequest in pendingRequests)
        {
            pendingRequest.Status = AppointmentChangeRequestStatus.Cancelled;
            pendingRequest.Reason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Klijent je povukao zahtev za termin."
                : request.Reason.Trim();
            pendingRequest.RespondedAtUtc = now;
            pendingRequest.UpdatedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await AddAuditLogAsync(
            appointment.Id,
            "WithdrawnByCustomer",
            string.IsNullOrWhiteSpace(request.Reason)
                ? "Klijent je povukao zahtev za termin."
                : $"Klijent je povukao zahtev za termin. Razlog: {request.Reason.Trim()}",
            $"Status={AppointmentStatus.PendingApproval};Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o}",
            $"Status={appointment.Status};Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o}",
            cancellationToken);

        await _chatSystemMessageService.SendCustomerWithdrawnAppointmentRequestToBusinessAsync(
    appointment,
    request.Reason,
    cancellationToken);

        return Ok(new AppointmentActionResponse
        {
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status.ToString(),
            Action = "WithdrawnByCustomer",
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            Message = "Zahtev za termin je povučen."
        });
    }

    [HttpPost("request-reschedule")]
    [ProducesResponseType(typeof(AppointmentChangeActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentChangeActionResponse>> RequestReschedule(
    [FromBody] RequestAppointmentRescheduleRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                "Promena termina može da se traži samo za potvrđen termin.",
                "appointment_not_confirmed"));
        }

        var proposedStart = request.ProposedStartAtUtc;
        var proposedEnd = proposedStart.AddMinutes((appointment.EndAtUtc - appointment.StartAtUtc).TotalMinutes);

        var scheduleValidationError = await ValidateAcceptedScheduleAsync(
            appointment,
            proposedStart,
            proposedEnd,
            cancellationToken);

        if (scheduleValidationError is not null)
            return BadRequest(BuildAppointmentOperationErrorResponse(scheduleValidationError));

        var existingPendingRescheduleRequests = await _dbContext.AppointmentChangeRequests
            .Where(x =>
                x.AppointmentId == appointment.Id &&
                x.Status == AppointmentChangeRequestStatus.Pending &&
                x.RequestType == AppointmentChangeRequestType.RescheduleRequest)
            .ToListAsync(cancellationToken);

        foreach (var item in existingPendingRescheduleRequests)
        {
            item.Status = AppointmentChangeRequestStatus.Cancelled;
            item.RespondedAtUtc = DateTime.UtcNow;
            item.UpdatedAtUtc = DateTime.UtcNow;
        }

        var now = DateTime.UtcNow;

        var changeRequest = new AppointmentChangeRequest
        {
            AppointmentId = appointment.Id,
            RequestType = AppointmentChangeRequestType.RescheduleRequest,
            Status = AppointmentChangeRequestStatus.Pending,
            InitiatedBy = ChangeInitiatorType.Customer,
            OriginalStartAtUtc = appointment.StartAtUtc,
            OriginalEndAtUtc = appointment.EndAtUtc,
            ProposedStartAtUtc = proposedStart,
            ProposedEndAtUtc = proposedEnd,
            Reason = "Customer requested reschedule",
            Message = request.Message?.Trim(),
            ExpiresAtUtc = GetConfirmedRescheduleRequestExpirationUtc(now),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.AppointmentChangeRequests.Add(changeRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await AddAuditLogAsync(
            appointment.Id,
            "RescheduleRequestedByCustomer",
            request.Message?.Trim() ?? "Klijent je zatražio promenu termina.",
            $"OldStart={appointment.StartAtUtc:o};OldEnd={appointment.EndAtUtc:o}",
            $"ProposedStart={changeRequest.ProposedStartAtUtc:o};ProposedEnd={changeRequest.ProposedEndAtUtc:o}",
            cancellationToken);

        await _chatSystemMessageService.SendCustomerRequestedRescheduleToBusinessAsync(
    appointment,
    changeRequest,
    cancellationToken);

        return Ok(new AppointmentChangeActionResponse
        {
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status.ToString(),
            ChangeRequestId = changeRequest.Id,
            ChangeRequestStatus = changeRequest.Status.ToString(),
            StartAtUtc = changeRequest.ProposedStartAtUtc,
            EndAtUtc = changeRequest.ProposedEndAtUtc,
            DurationMin = (int)(changeRequest.ProposedEndAtUtc - changeRequest.ProposedStartAtUtc).TotalMinutes,
            Message = "Zahtev za promenu termina je uspešno poslat."
        });
    }

    [HttpPost("accept-delay")]
    [ProducesResponseType(typeof(AppointmentChangeActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentChangeActionResponse>> AcceptDelay(
     [FromBody] AcceptDelayProposalRequest request,
     CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                "Ovaj termin nije potvrđen.",
                "appointment_not_confirmed"));
        }

        var changeRequest = await _dbContext.AppointmentChangeRequests
            .FirstOrDefaultAsync(
                x => x.Id == request.ChangeRequestId &&
                     x.AppointmentId == request.AppointmentId &&
                     x.RequestType == AppointmentChangeRequestType.DelayProposal,
                cancellationToken);

        if (changeRequest is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Predlog pomeranja ne postoji.",
                "delay_proposal_not_found"));
        }

        if (changeRequest.Status != AppointmentChangeRequestStatus.Pending)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                GetInactiveChangeRequestMessage(changeRequest, "Predlog pomeranja"),
                GetChangeRequestReasonCode(changeRequest, "delay_proposal")));
        }

        var scheduleValidationError = await ValidateAcceptedDelayScheduleAsync(
            appointment,
            changeRequest.ProposedStartAtUtc,
            changeRequest.ProposedEndAtUtc,
            cancellationToken);

        if (scheduleValidationError is not null)
            return BadRequest(BuildAppointmentOperationErrorResponse(scheduleValidationError));

        var now = DateTime.UtcNow;

        appointment.StartAtUtc = changeRequest.ProposedStartAtUtc;
        appointment.EndAtUtc = changeRequest.ProposedEndAtUtc;
        appointment.UpdatedAtUtc = now;

        changeRequest.Status = AppointmentChangeRequestStatus.Accepted;
        changeRequest.RespondedAtUtc = now;
        changeRequest.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await AddAuditLogAsync(
            appointment.Id,
            "DelayProposalAccepted",
            "Klijent je prihvatio pomeranje termina.",
            null,
            $"Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o}",
            cancellationToken);

        await _chatSystemMessageService.SendCustomerAcceptedProposalToBusinessAsync(
            appointment,
            changeRequest,
            cancellationToken);

        await _chatSystemMessageService.SendDelayAcceptedToCustomerAsync(
    appointment,
    changeRequest,
    cancellationToken);

        return Ok(new AppointmentChangeActionResponse
        {
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status.ToString(),
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            DurationMin = (int)(appointment.EndAtUtc - appointment.StartAtUtc).TotalMinutes,
            ChangeRequestId = changeRequest.Id,
            ChangeRequestStatus = changeRequest.Status.ToString(),
            Message = "Pomeranje termina je prihvaćeno."
        });
    }

    [HttpPost("reject-delay")]
    [ProducesResponseType(typeof(AppointmentChangeActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentChangeActionResponse>> RejectDelay(
     [FromBody] RejectDelayProposalRequest request,
     CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                "Ovaj termin nije potvrđen.",
                "appointment_not_confirmed"));
        }

        var changeRequest = await _dbContext.AppointmentChangeRequests
            .FirstOrDefaultAsync(
                x => x.Id == request.ChangeRequestId &&
                     x.AppointmentId == request.AppointmentId &&
                     x.RequestType == AppointmentChangeRequestType.DelayProposal,
                cancellationToken);

        if (changeRequest is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Predlog pomeranja ne postoji.",
                "delay_proposal_not_found"));
        }

        if (changeRequest.Status != AppointmentChangeRequestStatus.Pending)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                GetInactiveChangeRequestMessage(changeRequest, "Predlog pomeranja"),
                GetChangeRequestReasonCode(changeRequest, "delay_proposal")));
        }

        var now = DateTime.UtcNow;

        changeRequest.Status = AppointmentChangeRequestStatus.Rejected;
        changeRequest.Reason = request.Reason?.Trim();
        changeRequest.RespondedAtUtc = now;
        changeRequest.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await AddAuditLogAsync(
            appointment.Id,
            "DelayProposalRejected",
            request.Reason?.Trim() ?? "Klijent je odbio predlog pomeranja.",
            null,
            $"Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o}",
            cancellationToken);

        await _chatSystemMessageService.SendCustomerRejectedProposalToBusinessAsync(
            appointment,
            changeRequest,
            cancellationToken);

        await _chatSystemMessageService.SendDelayRejectedToCustomerAsync(
    appointment,
    changeRequest,
    cancellationToken);

        return Ok(new AppointmentChangeActionResponse
        {
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status.ToString(),
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            DurationMin = (int)(appointment.EndAtUtc - appointment.StartAtUtc).TotalMinutes,
            ChangeRequestId = changeRequest.Id,
            ChangeRequestStatus = changeRequest.Status.ToString(),
            Message = "Pomeranje termina je odbijeno."
        });
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

    private async Task ExpirePendingRequestsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var expiredRequests = await _dbContext.AppointmentChangeRequests
            .Where(x =>
                x.Status == AppointmentChangeRequestStatus.Pending &&
                x.ExpiresAtUtc.HasValue &&
                x.ExpiresAtUtc.Value <= now)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (expiredRequests.Count == 0)
            return;

        var appointmentIds = expiredRequests
            .Select(x => x.AppointmentId)
            .Distinct()
            .ToList();

        var appointments = await _dbContext.Appointments
            .Where(x => appointmentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var changeRequest in expiredRequests)
        {
            if (!appointments.TryGetValue(changeRequest.AppointmentId, out var appointment))
                continue;

            changeRequest.Status = AppointmentChangeRequestStatus.Expired;
            changeRequest.RespondedAtUtc = now;
            changeRequest.UpdatedAtUtc = now;

            string actionType;
            string message;
            string? oldValuesJson = null;
            string? newValuesJson = null;

            switch (changeRequest.RequestType)
            {
                case AppointmentChangeRequestType.NewBookingRequest:
                    appointment.Status = AppointmentStatus.Rejected;
                    appointment.UpdatedAtUtc = now;

                    actionType = "NewBookingRequestExpired";
                    message = "Zahtev za termin je istekao jer nije bilo odgovora na vreme.";
                    newValuesJson = $"Status={appointment.Status}";
                    break;

                case AppointmentChangeRequestType.CounterProposal:
                    appointment.Status = AppointmentStatus.Rejected;
                    appointment.UpdatedAtUtc = now;

                    actionType = "CounterProposalExpired";
                    message = "Predlog novog termina je istekao jer nije bilo odgovora na vreme.";
                    newValuesJson = $"Status={appointment.Status}";
                    break;

                case AppointmentChangeRequestType.DelayProposal:
                    actionType = "DelayProposalExpired";
                    message = "Predlog pomeranja je istekao, pa ostaje prethodno potvrđeni termin.";
                    oldValuesJson = $"ProposedStart={changeRequest.ProposedStartAtUtc:o};ProposedEnd={changeRequest.ProposedEndAtUtc:o}";
                    newValuesJson = $"Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o};Status={appointment.Status}";
                    break;

                default:
                    actionType = "ChangeRequestExpired";
                    message = "Zahtev više nije aktivan jer je istekao.";
                    break;
            }

            _dbContext.AppointmentAuditLogs.Add(new AppointmentAuditLog
            {
                AppointmentId = appointment.Id,
                ActionType = actionType,
                Message = message,
                OldValuesJson = oldValuesJson,
                NewValuesJson = newValuesJson,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }



    private async Task<string?> ValidateServiceResourceSelectionAsync(
    long businessId,
    long serviceId,
    long? resourceId,
    CancellationToken cancellationToken)
    {
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

    private static AppointmentOperationErrorResponse BuildAppointmentOperationErrorResponse(
    string message,
    string reasonCode,
    bool hasSlotGridViolation = false,
    bool hasBusinessHoursViolation = false,
    bool hasStaffHoursViolation = false,
    bool hasTimeOffConflict = false,
    bool hasAppointmentConflict = false,
    bool hasResourceConflict = false)
    {
        return new AppointmentOperationErrorResponse
        {
            Message = message,
            ReasonCode = reasonCode,
            ReasonCodes = string.IsNullOrWhiteSpace(reasonCode)
                ? new List<string>()
                : new List<string> { reasonCode },
            HasSlotGridViolation = hasSlotGridViolation,
            HasBusinessHoursViolation = hasBusinessHoursViolation,
            HasStaffHoursViolation = hasStaffHoursViolation,
            HasTimeOffConflict = hasTimeOffConflict,
            HasAppointmentConflict = hasAppointmentConflict,
            HasResourceConflict = hasResourceConflict
        };
    }

    private static AppointmentOperationErrorResponse BuildAppointmentOperationErrorResponse(
        OwnerCreateAvailabilityResult availability)
    {
        return new AppointmentOperationErrorResponse
        {
            Message = availability.Message,
            ReasonCode = availability.ReasonCode,
            ReasonCodes = availability.ReasonCodes,
            HasSlotGridViolation = availability.HasSlotGridViolation,
            HasBusinessHoursViolation = availability.HasBusinessHoursViolation,
            HasStaffHoursViolation = availability.HasStaffHoursViolation,
            HasTimeOffConflict = availability.HasTimeOffConflict,
            HasAppointmentConflict = availability.HasAppointmentConflict,
            HasResourceConflict = availability.HasResourceConflict
        };
    }

    private async Task<OwnerCreateAvailabilityResult> CheckAvailabilityViaServiceAsync(
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
        var serviceResult = await _appointmentSchedulingService.CheckAvailabilityAsync(
            businessId,
            serviceId,
            staffMemberId,
            resourceId,
            startAtUtc,
            endAtUtc,
            ignoreAppointmentId,
            ignoreWorkingHours,
            ignoreTimeOffBlocks,
            ignoreAppointmentConflicts,
            cancellationToken);

        return new OwnerCreateAvailabilityResult
        {
            IsAvailable = serviceResult.IsAvailable,
            WasAvailableByRules = serviceResult.IsAvailable,
            HasBusinessHoursViolation = serviceResult.HasBusinessHoursViolation,
            HasStaffHoursViolation = serviceResult.HasStaffHoursViolation,
            HasTimeOffConflict = serviceResult.HasTimeOffConflict,
            HasAppointmentConflict = serviceResult.HasAppointmentConflict,
            HasResourceConflict = serviceResult.HasResourceConflict,
            Message = serviceResult.Message,
            ReasonCode = serviceResult.ReasonCode,
            ReasonCodes = serviceResult.IsAvailable
                ? new List<string>()
                : new List<string> { serviceResult.ReasonCode }
        };
    }

    private sealed class OwnerCreateAvailabilityResult
    {
        public bool IsAvailable { get; set; }
        public bool WasAvailableByRules { get; set; }
        public bool HasSlotGridViolation { get; set; }
        public bool HasBusinessHoursViolation { get; set; }
        public bool HasStaffHoursViolation { get; set; }
        public bool HasTimeOffConflict { get; set; }
        public bool HasAppointmentConflict { get; set; }
        public bool HasResourceConflict { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ReasonCode { get; set; } = string.Empty;
        public List<string> ReasonCodes { get; set; } = new();
    }


    private static DateTime GetConfirmedRescheduleRequestExpirationUtc(DateTime nowUtc)
    {
        return nowUtc.AddHours(12);
    }

    private static DateTime GetNewBookingRequestExpirationUtc(DateTime nowUtc)
    {
        return nowUtc.AddHours(24);
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
    private static CreateAppointmentErrorResponse BuildCreateAppointmentErrorResponse(
    string message,
    string reasonCode,
    bool hasSlotGridViolation = false,
    bool hasBusinessHoursViolation = false,
    bool hasStaffHoursViolation = false,
    bool hasTimeOffConflict = false,
    bool hasAppointmentConflict = false,
    bool hasResourceConflict = false)
    {
        return new CreateAppointmentErrorResponse
        {
            Message = message,
            ReasonCode = reasonCode,
            ReasonCodes = string.IsNullOrWhiteSpace(reasonCode)
                ? new List<string>()
                : new List<string> { reasonCode },
            HasSlotGridViolation = hasSlotGridViolation,
            HasBusinessHoursViolation = hasBusinessHoursViolation,
            HasStaffHoursViolation = hasStaffHoursViolation,
            HasTimeOffConflict = hasTimeOffConflict,
            HasAppointmentConflict = hasAppointmentConflict,
            HasResourceConflict = hasResourceConflict
        };
    }

    private static CreateAppointmentErrorResponse BuildCreateAppointmentErrorResponse(
        OwnerCreateAvailabilityResult availability)
    {
        return new CreateAppointmentErrorResponse
        {
            Message = availability.Message,
            ReasonCode = availability.ReasonCode,
            ReasonCodes = availability.ReasonCodes,
            HasSlotGridViolation = availability.HasSlotGridViolation,
            HasBusinessHoursViolation = availability.HasBusinessHoursViolation,
            HasStaffHoursViolation = availability.HasStaffHoursViolation,
            HasTimeOffConflict = availability.HasTimeOffConflict,
            HasAppointmentConflict = availability.HasAppointmentConflict,
            HasResourceConflict = availability.HasResourceConflict
        };
    }

    private static string MapCreateResourceSelectionReasonCode(string message)
    {
        return message switch
        {
            "Za izabranu uslugu je potrebno izabrati resurs." => "resource_required",
            "Izabrani resurs ne postoji." => "resource_not_found",
            "Izabrani resurs nije aktivan." => "resource_inactive",
            "Izabrani resurs nije dozvoljen za izabranu uslugu." => "resource_not_allowed_for_service",
            _ => "invalid_resource_selection"
        };
    }

    private static string MapStaffServiceValidationReasonCode(string message)
    {
        return message switch
        {
            "Izabrani radnik ne postoji." => "staff_not_found",
            "Izabrani radnik ne pripada ovoj radnji." => "staff_not_in_business",
            "Izabrana usluga ne postoji." => "service_not_found",
            "Izabrana usluga ne pripada ovoj radnji." => "service_not_in_business",
            "Izabrani radnik ne radi ovu uslugu." => "staff_not_assigned_to_service",
            _ => "invalid_staff_service_selection"
        };
    }

    private async Task<OwnerCreateAvailabilityResult?> ValidateAcceptedDelayScheduleAsync(
    Appointment appointment,
    DateTime proposedStartAtUtc,
    DateTime proposedEndAtUtc,
    CancellationToken cancellationToken)
    {
        if (!appointment.PrimaryStaffMemberId.HasValue)
        {
            return new OwnerCreateAvailabilityResult
            {
                IsAvailable = false,
                Message = "Za termin je potrebno da bude izabran zaposleni.",
                ReasonCode = "staff_required",
                ReasonCodes = new List<string> { "staff_required" }
            };
        }

        var staffServiceValidationError = await StaffServiceValidationHelper.ValidateStaffCanPerformServiceAsync(
            _dbContext,
            appointment.BusinessId,
            appointment.ServiceId,
            appointment.PrimaryStaffMemberId,
            cancellationToken);

        if (staffServiceValidationError is not null)
        {
            return new OwnerCreateAvailabilityResult
            {
                IsAvailable = false,
                Message = staffServiceValidationError,
                ReasonCode = MapStaffServiceValidationReasonCode(staffServiceValidationError),
                ReasonCodes = new List<string> { MapStaffServiceValidationReasonCode(staffServiceValidationError) }
            };
        }

        var availability = await CheckAvailabilityViaServiceAsync(
            appointment.BusinessId,
            appointment.ServiceId,
            appointment.PrimaryStaffMemberId.Value,
            appointment.ResourceId,
            proposedStartAtUtc,
            proposedEndAtUtc,
            appointment.Id,
            false,
            false,
            false,
            cancellationToken);

        if (!availability.IsAvailable)
            return availability;

        return null;
    }

    private static string MapStaffResourceValidationReasonCode(string message)
    {
        return message switch
        {
            "Izabrani radnik ne postoji." => "staff_not_found",
            "Izabrani radnik ne pripada ovoj radnji." => "staff_not_in_business",
            "Izabrani resurs ne postoji." => "resource_not_found",
            "Izabrani resurs ne pripada ovoj radnji." => "resource_not_in_business",
            "Izabrani radnik ne radi sa ovim resursom." => "staff_not_assigned_to_resource",
            _ => "invalid_staff_resource_selection"
        };
    }

    private async Task<OwnerCreateAvailabilityResult?> ValidateAcceptedScheduleAsync(
        Appointment appointment,
        DateTime proposedStartAtUtc,
        DateTime proposedEndAtUtc,
        CancellationToken cancellationToken)
    {
        if (!appointment.PrimaryStaffMemberId.HasValue)
        {
            return new OwnerCreateAvailabilityResult
            {
                IsAvailable = false,
                Message = "Za termin je potrebno da bude izabran zaposleni.",
                ReasonCode = "staff_required",
                ReasonCodes = new List<string> { "staff_required" }
            };
        }

        var staffServiceValidationError = await StaffServiceValidationHelper.ValidateStaffCanPerformServiceAsync(
            _dbContext,
            appointment.BusinessId,
            appointment.ServiceId,
            appointment.PrimaryStaffMemberId,
            cancellationToken);

        if (staffServiceValidationError is not null)
        {
            return new OwnerCreateAvailabilityResult
            {
                IsAvailable = false,
                Message = staffServiceValidationError,
                ReasonCode = MapStaffServiceValidationReasonCode(staffServiceValidationError),
                ReasonCodes = new List<string> { MapStaffServiceValidationReasonCode(staffServiceValidationError) }
            };
        }

        var availability = await CheckAvailabilityViaServiceAsync(
            appointment.BusinessId,
            appointment.ServiceId,
            appointment.PrimaryStaffMemberId.Value,
            appointment.ResourceId,
            proposedStartAtUtc,
            proposedEndAtUtc,
            appointment.Id,
            false,
            false,
            false,
            cancellationToken);

        if (!availability.IsAvailable)
            return availability;

        return null;
    }

    private long? TryGetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out var userId) ? userId : null;
    }
}