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
    public async Task<ActionResult<List<AppointmentListItemResponse>>> GetAll(
    [FromQuery] long? businessId,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var query = _dbContext.Appointments.AsNoTracking();

        if (businessId.HasValue)
            query = query.Where(x => x.BusinessId == businessId.Value);

        var items = await query
            .OrderBy(x => x.StartAtUtc)
.Select(x => new AppointmentListItemResponse
{
    Id = x.Id,
    BusinessId = x.BusinessId,
    ServiceId = x.ServiceId,
    PrimaryStaffMemberId = x.PrimaryStaffMemberId,
    CustomerName = x.CustomerName,
    CustomerPhone = x.CustomerPhone,
    Status = x.Status.ToString(),
    StartAtUtc = x.StartAtUtc,
    EndAtUtc = x.EndAtUtc,
    Notes = x.Notes
})
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost]
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
            return BadRequest("Izabrana usluga ne postoji.");

        if (request.PrimaryStaffMemberId.HasValue)
        {
            var staffExists = await _dbContext.StaffMembers
                .AnyAsync(
                    x => x.Id == request.PrimaryStaffMemberId.Value && x.BusinessId == request.BusinessId,
                    cancellationToken);

            if (!staffExists)
                return BadRequest("Izabrani zaposleni ne postoji.");
        }

        var totalDurationMin = await GetTotalServiceDurationAsync(
            service.Id,
            service.EstimatedDurationMin,
            cancellationToken);

        var proposedStart = request.StartAtUtc;
        var proposedEnd = request.StartAtUtc.AddMinutes(totalDurationMin);

        if (!request.PrimaryStaffMemberId.HasValue)
            return BadRequest("Potrebno je izabrati zaposlenog.");

        var isAlignedToSlotGrid = await IsStartAlignedToBusinessSlotGridAsync(
            request.BusinessId,
            request.PrimaryStaffMemberId.Value,
            proposedStart,
            cancellationToken);

        if (!isAlignedToSlotGrid)
            return BadRequest("Izabrani početak termina nije dostupan. Izaberite ponuđeni termin.");

        var availability = await IsSpecificTimeAvailableAsync(
            request.BusinessId,
            request.PrimaryStaffMemberId.Value,
            proposedStart,
            proposedEnd,
            null,
            ignoreWorkingHours: false,
            ignoreTimeOffBlocks: false,
            ignoreAppointmentConflicts: false,
            cancellationToken);

        if (!availability.IsAvailable)
            return BadRequest("Izabrani termin nije dostupan.");

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
            ExpiresAtUtc = GetNewBookingRequestExpirationUtc(now),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.AppointmentChangeRequests.Add(changeRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new CreateAppointmentResponse
        {
            Id = appointment.Id,
            BusinessId = appointment.BusinessId,
            ServiceId = appointment.ServiceId,
            PrimaryStaffMemberId = appointment.PrimaryStaffMemberId,
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

    [HttpPost("owner-create")]
    public async Task<ActionResult<OwnerCreateAppointmentResponse>> CreateOwnerAppointment(
 [FromBody] CreateOwnerAppointmentRequest request,
 CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var service = await _dbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.ServiceId && x.BusinessId == request.BusinessId,
                cancellationToken);

        if (service is null)
            return BadRequest("Izabrana usluga ne postoji.");

        if (request.PrimaryStaffMemberId.HasValue)
        {
            var staffExists = await _dbContext.StaffMembers
                .AnyAsync(
                    x => x.Id == request.PrimaryStaffMemberId.Value && x.BusinessId == request.BusinessId,
                    cancellationToken);

            if (!staffExists)
                return BadRequest("Izabrani zaposleni ne postoji.");
        }

        if (string.IsNullOrWhiteSpace(request.CustomerName))
            return BadRequest("Unesite ime klijenta.");

        if (string.IsNullOrWhiteSpace(request.CustomerPhone))
            return BadRequest("Unesite broj telefona klijenta.");

        var defaultDurationMin = await GetTotalServiceDurationAsync(
            service.Id,
            service.EstimatedDurationMin,
            cancellationToken);

        if (request.FinalDurationMin.HasValue)
        {
            if (request.FinalDurationMin.Value <= 0)
                return BadRequest("Trajanje termina mora biti veće od 0 minuta.");

            if (request.FinalDurationMin.Value % 5 != 0)
                return BadRequest("Trajanje termina mora biti zadato u koracima od 5 minuta.");
        }

        var effectiveDurationMin = request.FinalDurationMin ?? defaultDurationMin;
        var startAtUtc = request.StartAtUtc;
        var endAtUtc = startAtUtc.AddMinutes(effectiveDurationMin);
        var useLegacyMasterOverride = request.IgnoreAvailabilityRules;
        var effectiveIgnoreWorkingHours = useLegacyMasterOverride || request.IgnoreWorkingHours;
        var effectiveIgnoreTimeOffBlocks = useLegacyMasterOverride || request.IgnoreTimeOffBlocks;
        var effectiveIgnoreAppointmentConflicts = useLegacyMasterOverride || request.IgnoreAppointmentConflicts;

        var effectiveGridOverride =
            useLegacyMasterOverride ||
            effectiveIgnoreWorkingHours ||
            effectiveIgnoreTimeOffBlocks ||
            effectiveIgnoreAppointmentConflicts;

        var hasSlotGridViolation = false;

        if (!request.PrimaryStaffMemberId.HasValue)
            return BadRequest("Potrebno je izabrati zaposlenog.");

        var isAlignedToSlotGrid = await IsStartAlignedToBusinessSlotGridAsync(
            request.BusinessId,
            request.PrimaryStaffMemberId.Value,
            startAtUtc,
            cancellationToken);

        if (!isAlignedToSlotGrid)
        {
            hasSlotGridViolation = true;

            if (!effectiveGridOverride)
            {
                return BadRequest(new OwnerCreateAvailabilityErrorResponse
                {
                    Message = "Izabrani početak termina nije na rasporedu radnje.",
                    ReasonCode = "outside_slot_grid",
                    ReasonCodes = new List<string> { "outside_slot_grid" },
                    HasSlotGridViolation = true,
                    HasBusinessHoursViolation = false,
                    HasStaffHoursViolation = false,
                    HasTimeOffConflict = false,
                    HasAppointmentConflict = false,
                    BypassedSlotGrid = false,
                    BypassedWorkingHours = false,
                    BypassedTimeOffBlocks = false,
                    BypassedAppointmentConflicts = false,
                    EffectiveIgnoreWorkingHours = effectiveIgnoreWorkingHours,
                    EffectiveIgnoreTimeOffBlocks = effectiveIgnoreTimeOffBlocks,
                    EffectiveIgnoreAppointmentConflicts = effectiveIgnoreAppointmentConflicts,
                    LegacyMasterOverride = useLegacyMasterOverride,
                    AppliedOverrides = new List<string>(),
                    AppliedOverrideLabels = new List<string>()
                });
            }
        }
        var availability = await IsOwnerCreateTimeAvailableAsync(
            request,
            endAtUtc,
            cancellationToken);
        availability.HasSlotGridViolation = hasSlotGridViolation;
        var bypassedSlotGrid =
    availability.HasSlotGridViolation &&
    effectiveGridOverride;
        var bypassedWorkingHours =
    (availability.HasBusinessHoursViolation || availability.HasStaffHoursViolation) &&
    effectiveIgnoreWorkingHours;

        var bypassedTimeOffBlocks =
            availability.HasTimeOffConflict &&
            effectiveIgnoreTimeOffBlocks;

        var bypassedAppointmentConflicts =
            availability.HasAppointmentConflict &&
            effectiveIgnoreAppointmentConflicts;

        var appliedOverrides = GetOwnerCreateAppliedOverrides(
            bypassedSlotGrid,
            bypassedWorkingHours,
            bypassedTimeOffBlocks,
            bypassedAppointmentConflicts);
        var appliedOverrideLabels = GetOwnerCreateAppliedOverrideLabels(
            bypassedSlotGrid,
            bypassedWorkingHours,
            bypassedTimeOffBlocks,
            bypassedAppointmentConflicts); ;

        var creationMode = GetOwnerCreateCreationMode(
            bypassedSlotGrid,
            bypassedWorkingHours,
            bypassedTimeOffBlocks,
            bypassedAppointmentConflicts);

        var creationModeLabel = GetOwnerCreateCreationModeLabel(creationMode);


        if (!availability.IsAvailable)
        {
            return BadRequest(new OwnerCreateAvailabilityErrorResponse
            {
                Message = availability.Message,
                ReasonCode = availability.ReasonCode,
                ReasonCodes = availability.ReasonCodes,
                HasSlotGridViolation = availability.HasSlotGridViolation,
                HasBusinessHoursViolation = availability.HasBusinessHoursViolation,
                HasStaffHoursViolation = availability.HasStaffHoursViolation,
                HasTimeOffConflict = availability.HasTimeOffConflict,
                HasAppointmentConflict = availability.HasAppointmentConflict,
                BypassedSlotGrid = bypassedSlotGrid,
                BypassedWorkingHours = bypassedWorkingHours,
                BypassedTimeOffBlocks = bypassedTimeOffBlocks,
                BypassedAppointmentConflicts = bypassedAppointmentConflicts,
                EffectiveIgnoreWorkingHours = request.IgnoreAvailabilityRules || request.IgnoreWorkingHours,
                EffectiveIgnoreTimeOffBlocks = request.IgnoreAvailabilityRules || request.IgnoreTimeOffBlocks,
                EffectiveIgnoreAppointmentConflicts = request.IgnoreAvailabilityRules || request.IgnoreAppointmentConflicts,
                LegacyMasterOverride = useLegacyMasterOverride,
                AppliedOverrides = appliedOverrides,
                AppliedOverrideLabels = appliedOverrideLabels
            });
        }


        var now = DateTime.UtcNow;

        var appointment = new Appointment
        {
            BusinessId = request.BusinessId,
            ServiceId = request.ServiceId,
            PrimaryStaffMemberId = request.PrimaryStaffMemberId,
            CustomerName = request.CustomerName.Trim(),
            CustomerPhone = request.CustomerPhone.Trim(),
            StartAtUtc = startAtUtc,
            EndAtUtc = endAtUtc,
            Status = AppointmentStatus.Confirmed,
            Notes = request.Notes?.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.Appointments.Add(appointment);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await AddAuditLogAsync(
            appointment.Id,
            "OwnerCreatedConfirmedAppointment",
            BuildOwnerCreateAuditMessage(
                availability.WasAvailableByRules,
                bypassedSlotGrid,
                bypassedWorkingHours,
                bypassedTimeOffBlocks,
                bypassedAppointmentConflicts),
            null,
            $"Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o};Status={appointment.Status};" +
            $"FinalDurationMin={request.FinalDurationMin};EffectiveDurationMin={effectiveDurationMin};" +
            $"IgnoreAvailabilityRules={request.IgnoreAvailabilityRules};LegacyMasterOverride={useLegacyMasterOverride};" +
            $"IgnoreWorkingHours={effectiveIgnoreWorkingHours};IgnoreTimeOffBlocks={effectiveIgnoreTimeOffBlocks};" +
            $"HasSlotGridViolation={availability.HasSlotGridViolation};BypassedSlotGrid={bypassedSlotGrid};" +
            $"IgnoreAppointmentConflicts={effectiveIgnoreAppointmentConflicts};" +
            $"HasBusinessHoursViolation={availability.HasBusinessHoursViolation};HasStaffHoursViolation={availability.HasStaffHoursViolation};" +
            $"HasTimeOffConflict={availability.HasTimeOffConflict};HasAppointmentConflict={availability.HasAppointmentConflict};" +
            $"BypassedWorkingHours={bypassedWorkingHours};BypassedTimeOffBlocks={bypassedTimeOffBlocks};" +
            $"BypassedAppointmentConflicts={bypassedAppointmentConflicts};" +
            $"WasAvailableByRules={availability.WasAvailableByRules}",
            cancellationToken);

        return Ok(new OwnerCreateAppointmentResponse
        {
            Id = appointment.Id,
            BusinessId = appointment.BusinessId,
            ServiceId = appointment.ServiceId,
            PrimaryStaffMemberId = appointment.PrimaryStaffMemberId,
            CustomerName = appointment.CustomerName,
            CustomerPhone = appointment.CustomerPhone,
            Status = appointment.Status.ToString(),
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            LegacyMasterOverride = useLegacyMasterOverride,
            IgnoreAvailabilityRules = request.IgnoreAvailabilityRules,
            EffectiveIgnoreWorkingHours = effectiveIgnoreWorkingHours,
            EffectiveIgnoreTimeOffBlocks = effectiveIgnoreTimeOffBlocks,
            EffectiveIgnoreAppointmentConflicts = effectiveIgnoreAppointmentConflicts,
            HasBusinessHoursViolation = availability.HasBusinessHoursViolation,
            HasStaffHoursViolation = availability.HasStaffHoursViolation,
            HasTimeOffConflict = availability.HasTimeOffConflict,
            HasAppointmentConflict = availability.HasAppointmentConflict,
            BypassedWorkingHours = bypassedWorkingHours,
            BypassedTimeOffBlocks = bypassedTimeOffBlocks,
            BypassedAppointmentConflicts = bypassedAppointmentConflicts,
            WasAvailableByRules = availability.WasAvailableByRules,
            CreationMode = creationMode,
            CreationModeLabel = creationModeLabel,
            AppliedOverrides = appliedOverrides,
            AppliedOverrideLabels = appliedOverrideLabels,
            Message = BuildOwnerCreateResponseMessage(
                availability.WasAvailableByRules,
                bypassedSlotGrid,
                bypassedWorkingHours,
                bypassedTimeOffBlocks,
                bypassedAppointmentConflicts),
            ReasonCodes = availability.ReasonCodes
        });
    }

    [HttpPost("approve")]
    public async Task<ActionResult<AppointmentActionResponse>> Approve(
[FromBody] ApproveAppointmentRequest request,
CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            return NotFound("Termin ne postoji.");

        if (appointment.Status != AppointmentStatus.PendingApproval)
            return BadRequest("Ovaj termin više ne čeka potvrdu.");

        if (request.FinalDurationMin.HasValue)
        {
            if (request.FinalDurationMin.Value <= 0)
                return BadRequest("Trajanje termina mora biti veće od 0 minuta.");

            if (request.FinalDurationMin.Value % 5 != 0)
                return BadRequest("Trajanje termina mora biti zadato u koracima od 5 minuta.");

            if (!appointment.PrimaryStaffMemberId.HasValue)
                return BadRequest("Za potvrdu termina je potrebno da termin ima izabranog zaposlenog.");

            var proposedEndAtUtc = appointment.StartAtUtc.AddMinutes(request.FinalDurationMin.Value);

            var availability = await IsSpecificTimeAvailableAsync(
                appointment.BusinessId,
                appointment.PrimaryStaffMemberId.Value,
                appointment.StartAtUtc,
                proposedEndAtUtc,
                appointment.Id,
                ignoreWorkingHours: false,
                ignoreTimeOffBlocks: false,
                ignoreAppointmentConflicts: false,
                cancellationToken);

            if (!availability.IsAvailable)
                return BadRequest(availability.Message);

            appointment.EndAtUtc = proposedEndAtUtc;
            appointment.UpdatedAtUtc = DateTime.UtcNow;

            var pendingRequestForDurationUpdate = await GetLatestPendingChangeRequestAsync(
                appointment.Id,
                cancellationToken);

            if (pendingRequestForDurationUpdate is not null)
            {
                pendingRequestForDurationUpdate.ProposedEndAtUtc = proposedEndAtUtc;
                pendingRequestForDurationUpdate.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await ApprovePendingAppointmentAsync(
            appointment,
            cancellationToken);

        await AddAuditLogAsync(
            appointment.Id,
            "Approved",
            request.FinalDurationMin.HasValue
                ? $"Termin je potvrđen. Trajanje je postavljeno na {request.FinalDurationMin.Value} minuta."
                : "Termin je potvrđen.",
            null,
            $"Status={appointment.Status};Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o};FinalDurationMin={request.FinalDurationMin}",
            cancellationToken);

        return Ok(new AppointmentActionResponse
        {
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status.ToString(),
            Action = "Approved",
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            Message = request.FinalDurationMin.HasValue
                ? $"Termin je potvrđen. Trajanje je postavljeno na {request.FinalDurationMin.Value} minuta."
                : "Termin je potvrđen."
        });
    }
    [HttpPost("reject")]
    public async Task<ActionResult<AppointmentActionResponse>> Reject(
    [FromBody] RejectAppointmentRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            return NotFound("Termin ne postoji.");

        if (appointment.Status != AppointmentStatus.PendingApproval)
            return BadRequest("Ovaj termin više ne čeka potvrdu.");

        await RejectPendingAppointmentAsync(
    appointment,
    request.Reason,
    cancellationToken);
        await AddAuditLogAsync(
    appointment.Id,
    "Rejected",
    request.Reason?.Trim() ?? "Termin je odbijen.",
    null,
    $"Status={appointment.Status}",
    cancellationToken);

        return Ok(new AppointmentActionResponse
        {
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status.ToString(),
            Message = "Termin je odbijen."
        });
    }

    [HttpPost("propose-time")]
    public async Task<ActionResult<AppointmentChangeActionResponse>> ProposeTime(
 [FromBody] ProposeAppointmentTimeRequest request,
 CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            return NotFound("Termin ne postoji.");

        if (appointment.Status != AppointmentStatus.PendingApproval)
            return BadRequest("Novi termin može da se predloži samo dok zahtev još čeka potvrdu.");

        var service = await _dbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == appointment.ServiceId && x.BusinessId == appointment.BusinessId,
                cancellationToken);

        if (service is null)
            return BadRequest("Izabrana usluga ne postoji.");

        var defaultDurationMin = await GetTotalServiceDurationAsync(
            service.Id,
            service.EstimatedDurationMin,
            cancellationToken);

        if (request.FinalDurationMin.HasValue)
        {
            if (request.FinalDurationMin.Value <= 0)
                return BadRequest("Trajanje termina mora biti veće od 0 minuta.");

            if (request.FinalDurationMin.Value % 5 != 0)
                return BadRequest("Trajanje termina mora biti zadato u koracima od 5 minuta.");
        }

        if (!appointment.PrimaryStaffMemberId.HasValue)
            return BadRequest("Za predlog novog termina je potrebno da termin ima izabranog zaposlenog.");

        var effectiveDurationMin = request.FinalDurationMin ?? defaultDurationMin;
        var proposedStart = request.ProposedStartAtUtc;
        var proposedEnd = proposedStart.AddMinutes(effectiveDurationMin);

        var isAlignedToSlotGrid = await IsStartAlignedToBusinessSlotGridAsync(
            appointment.BusinessId,
            appointment.PrimaryStaffMemberId.Value,
            proposedStart,
            cancellationToken);

        if (!isAlignedToSlotGrid)
            return BadRequest("Predloženi početak termina nije na rasporedu radnje.");

        var availability = await IsSpecificTimeAvailableAsync(
            appointment.BusinessId,
            appointment.PrimaryStaffMemberId.Value,
            proposedStart,
            proposedEnd,
            appointment.Id,
            ignoreWorkingHours: false,
            ignoreTimeOffBlocks: false,
            ignoreAppointmentConflicts: false,
            cancellationToken);

        if (!availability.IsAvailable)
            return BadRequest(availability.Message);

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

        var now = DateTime.UtcNow;

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
            ExpiresAtUtc = GetCounterProposalExpirationUtc(now),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.AppointmentChangeRequests.Add(changeRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await AddAuditLogAsync(
            appointment.Id,
            "CounterProposalCreated",
            request.FinalDurationMin.HasValue
                ? request.Message?.Trim() ?? $"Predložen je novi termin sa trajanjem od {request.FinalDurationMin.Value} minuta."
                : request.Message?.Trim() ?? "Predložen je novi termin.",
            $"OldStart={appointment.StartAtUtc:o};OldEnd={appointment.EndAtUtc:o}",
            $"ProposedStart={changeRequest.ProposedStartAtUtc:o};ProposedEnd={changeRequest.ProposedEndAtUtc:o};FinalDurationMin={request.FinalDurationMin};EffectiveDurationMin={effectiveDurationMin}",
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
            Message = request.FinalDurationMin.HasValue
                ? $"Predložen je novi termin sa trajanjem od {request.FinalDurationMin.Value} minuta."
                : "Predložen je novi termin."
        });
    }

    [HttpPost("accept-proposal")]
    public async Task<ActionResult<AppointmentChangeActionResponse>> AcceptProposal(
    [FromBody] AcceptAppointmentProposalRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            return NotFound("Termin ne postoji.");

        var changeRequest = await _dbContext.AppointmentChangeRequests
            .FirstOrDefaultAsync(
                x => x.Id == request.ChangeRequestId && x.AppointmentId == request.AppointmentId,
                cancellationToken);

        if (changeRequest is null)
            return NotFound("Predlog promene ne postoji.");

        if (changeRequest.Status != AppointmentChangeRequestStatus.Pending)
            return BadRequest(GetInactiveChangeRequestMessage(changeRequest, "Predlog termina"));

        appointment.StartAtUtc = changeRequest.ProposedStartAtUtc;
        appointment.EndAtUtc = changeRequest.ProposedEndAtUtc;
        appointment.Status = AppointmentStatus.Confirmed;
        appointment.UpdatedAtUtc = DateTime.UtcNow;

        changeRequest.Status = AppointmentChangeRequestStatus.Accepted;
        changeRequest.RespondedAtUtc = DateTime.UtcNow;
        changeRequest.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await AddAuditLogAsync(
    appointment.Id,
    "CounterProposalAccepted",
    "Klijent je prihvatio novi termin.",
    null,
    $"Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o};Status={appointment.Status}",
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
    public async Task<ActionResult<AppointmentChangeActionResponse>> RejectProposal(
    [FromBody] RejectAppointmentProposalRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            return NotFound("Termin ne postoji.");

        var changeRequest = await _dbContext.AppointmentChangeRequests
            .FirstOrDefaultAsync(
                x => x.Id == request.ChangeRequestId && x.AppointmentId == request.AppointmentId,
                cancellationToken);

        if (changeRequest is null)
            return NotFound("Predlog promene ne postoji.");

        if (changeRequest.Status != AppointmentChangeRequestStatus.Pending)
            return BadRequest(GetInactiveChangeRequestMessage(changeRequest, "Predlog termina"));

        changeRequest.Status = AppointmentChangeRequestStatus.Rejected;
        changeRequest.Reason = request.Reason?.Trim();
        changeRequest.RespondedAtUtc = DateTime.UtcNow;
        changeRequest.UpdatedAtUtc = DateTime.UtcNow;

        appointment.Status = AppointmentStatus.Rejected;
        appointment.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await AddAuditLogAsync(
    appointment.Id,
    "CounterProposalRejected",
    request.Reason?.Trim() ?? "Klijent je odbio novi termin.",
    null,
    $"Status={appointment.Status}",
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

    [HttpGet("change-requests")]
    public async Task<ActionResult<List<AppointmentChangeRequestItemResponse>>> GetChangeRequests(
    [FromQuery] long appointmentId,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var items = await _dbContext.AppointmentChangeRequests
            .AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId)
            .OrderBy(x => x.CreatedAtUtc)
.Select(x => new AppointmentChangeRequestItemResponse
{
    Id = x.Id,
    AppointmentId = x.AppointmentId,
    RequestType = x.RequestType.ToString(),
    Status = x.Status.ToString(),
    InitiatedBy = x.InitiatedBy.ToString(),
    OriginalStartAtUtc = x.OriginalStartAtUtc,
    OriginalEndAtUtc = x.OriginalEndAtUtc,
    ProposedStartAtUtc = x.ProposedStartAtUtc,
    ProposedEndAtUtc = x.ProposedEndAtUtc,
    Reason = x.Reason,
    Message = x.Message,
    CreatedAtUtc = x.CreatedAtUtc,
    ExpiresAtUtc = x.ExpiresAtUtc,
    RespondedAtUtc = x.RespondedAtUtc
})
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("propose-delay")]
    public async Task<ActionResult<AppointmentChangeActionResponse>> ProposeDelay(
    [FromBody] ProposeDelayRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            return NotFound("Termin ne postoji.");

        if (appointment.Status != AppointmentStatus.Confirmed)
            return BadRequest("Pomeranje može da se predloži samo za potvrđen termin.");

        if (request.DelayMinutes <= 0)
            return BadRequest("Pomeranje mora biti veće od 0 minuta.");

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
            return BadRequest("Predloženo pomeranje upada u drugi zauzet termin.");

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
            ExpiresAtUtc = GetDelayProposalExpirationUtc(now),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.AppointmentChangeRequests.Add(changeRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await AddAuditLogAsync(
    appointment.Id,
    "DelayProposalCreated",
    request.Message?.Trim() ?? $"Predloženo je pomeranje termina za {request.DelayMinutes} minuta.",
    $"OldStart={appointment.StartAtUtc:o};OldEnd={appointment.EndAtUtc:o}",
    $"ProposedStart={changeRequest.ProposedStartAtUtc:o};ProposedEnd={changeRequest.ProposedEndAtUtc:o}",
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
            Message = "Predloženo je pomeranje termina."
        });
    }

    [HttpPost("accept-delay")]
    public async Task<ActionResult<AppointmentChangeActionResponse>> AcceptDelay(
    [FromBody] AcceptDelayProposalRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            return NotFound("Termin ne postoji.");

        if (appointment.Status != AppointmentStatus.Confirmed)
            return BadRequest("Ovaj termin nije potvrđen.");

        var changeRequest = await _dbContext.AppointmentChangeRequests
            .FirstOrDefaultAsync(
                x => x.Id == request.ChangeRequestId &&
                     x.AppointmentId == request.AppointmentId &&
                     x.RequestType == AppointmentChangeRequestType.DelayProposal,
                cancellationToken);

        if (changeRequest is null)
            return NotFound("Predlog pomeranja ne postoji.");

        if (changeRequest.Status != AppointmentChangeRequestStatus.Pending)
            return BadRequest(GetInactiveChangeRequestMessage(changeRequest, "Predlog pomeranja"));

        appointment.StartAtUtc = changeRequest.ProposedStartAtUtc;
        appointment.EndAtUtc = changeRequest.ProposedEndAtUtc;
        appointment.UpdatedAtUtc = DateTime.UtcNow;

        changeRequest.Status = AppointmentChangeRequestStatus.Accepted;
        changeRequest.RespondedAtUtc = DateTime.UtcNow;
        changeRequest.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await AddAuditLogAsync(
    appointment.Id,
    "DelayProposalAccepted",
    "Klijent je prihvatio pomeranje termina.",
    null,
    $"Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o}",
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
    public async Task<ActionResult<AppointmentChangeActionResponse>> RejectDelay(
    [FromBody] RejectDelayProposalRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            return NotFound("Termin ne postoji.");

        if (appointment.Status != AppointmentStatus.Confirmed)
            return BadRequest("Ovaj termin nije potvrđen.");

        var changeRequest = await _dbContext.AppointmentChangeRequests
            .FirstOrDefaultAsync(
                x => x.Id == request.ChangeRequestId &&
                     x.AppointmentId == request.AppointmentId &&
                     x.RequestType == AppointmentChangeRequestType.DelayProposal,
                cancellationToken);

        if (changeRequest is null)
            return NotFound("Predlog pomeranja ne postoji.");

        if (changeRequest.Status != AppointmentChangeRequestStatus.Pending)
            return BadRequest(GetInactiveChangeRequestMessage(changeRequest, "Predlog pomeranja"));

        changeRequest.Status = AppointmentChangeRequestStatus.Rejected;
        changeRequest.Reason = request.Reason?.Trim();
        changeRequest.RespondedAtUtc = DateTime.UtcNow;
        changeRequest.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await AddAuditLogAsync(
    appointment.Id,
    "DelayProposalRejected",
    request.Reason?.Trim() ?? "Klijent je odbio predlog pomeranja.",
    null,
    $"Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o}",
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

    [HttpPost("mark-call-customer")]
    public async Task<ActionResult<AppointmentActionResponse>> MarkCallCustomer(
    [FromBody] MarkCallCustomerRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        
        Appointment? appointment;

        try
        {
            appointment = await GetActiveAppointmentForOwnerCallActionAsync(
                request.AppointmentId,
                "Za ovaj termin više nije potrebno pozivanje klijenta.",
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        if (appointment is null)
            return NotFound("Termin ne postoji.");

        var message = BuildOwnerCallMessage(
            "Potrebno je pozvati klijenta.",
            request.Note);

        await AddAuditLogAsync(
            appointment.Id,
            "CallCustomerMarked",
            message,
            null,
            $"Status={appointment.Status};CustomerName={appointment.CustomerName};CustomerPhone={appointment.CustomerPhone}",
            cancellationToken);

        return Ok(new AppointmentActionResponse
        {
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status.ToString(),
            Action = "CallCustomerMarked",
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            Message = "Označeno je da klijenta treba pozvati."
        });
    }

    [HttpPost("mark-no-answer")]
    public async Task<ActionResult<AppointmentActionResponse>> MarkNoAnswer(
    [FromBody] MarkAppointmentCallActionRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        Appointment? appointment;

        try
        {
            appointment = await GetActiveAppointmentForOwnerCallActionAsync(
                request.AppointmentId,
                "Ova radnja više nije moguća jer termin nije aktivan.",
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        if (appointment is null)
            return NotFound("Termin ne postoji.");

        var message = BuildOwnerCallMessage(
            "Klijent se nije javio na poziv.",
    request.Note);

        await AddAuditLogAsync(
            appointment.Id,
            "CustomerCallNoAnswer",
            message,
            null,
            $"Status={appointment.Status};CustomerName={appointment.CustomerName};CustomerPhone={appointment.CustomerPhone}",
            cancellationToken);

        return Ok(new AppointmentActionResponse
        {
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status.ToString(),
            Action = "CustomerCallNoAnswer",
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            Message = "Zabeleženo je da se klijent nije javio."
        });
    }

    [HttpPost("mark-called-confirmed")]
    public async Task<ActionResult<AppointmentActionResponse>> MarkCalledConfirmed(
[FromBody] MarkAppointmentCallActionRequest request,
CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        Appointment? appointment;

        try
        {
            appointment = await GetActiveAppointmentForOwnerCallActionAsync(
                request.AppointmentId,
                "Potvrda pozivom više nije moguća jer termin nije aktivan.",
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        if (appointment is null)
            return NotFound("Termin ne postoji.");

        if (request.FinalDurationMin.HasValue)
        {
            if (request.FinalDurationMin.Value <= 0)
                return BadRequest("Trajanje termina mora biti veće od 0 minuta.");

            if (request.FinalDurationMin.Value % 5 != 0)
                return BadRequest("Trajanje termina mora biti zadato u koracima od 5 minuta.");

            if (!appointment.PrimaryStaffMemberId.HasValue)
                return BadRequest("Za potvrdu termina je potrebno da termin ima izabranog zaposlenog.");

            var proposedEndAtUtc = appointment.StartAtUtc.AddMinutes(request.FinalDurationMin.Value);

            var availability = await IsSpecificTimeAvailableAsync(
                appointment.BusinessId,
                appointment.PrimaryStaffMemberId.Value,
                appointment.StartAtUtc,
                proposedEndAtUtc,
                appointment.Id,
                ignoreWorkingHours: false,
                ignoreTimeOffBlocks: false,
                ignoreAppointmentConflicts: false,
                cancellationToken);

            if (!availability.IsAvailable)
                return BadRequest(availability.Message);

            appointment.EndAtUtc = proposedEndAtUtc;
            appointment.UpdatedAtUtc = DateTime.UtcNow;

            var pendingRequestForDurationUpdate = await GetLatestPendingChangeRequestAsync(
                appointment.Id,
                cancellationToken);

            if (pendingRequestForDurationUpdate is not null)
            {
                pendingRequestForDurationUpdate.ProposedEndAtUtc = proposedEndAtUtc;
                pendingRequestForDurationUpdate.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        var note = request.Note?.Trim();

        if (appointment.Status == AppointmentStatus.PendingApproval)
        {
            await ApprovePendingAppointmentAsync(
                appointment,
                cancellationToken);

            await AddAuditLogAsync(
                appointment.Id,
                "ConfirmedByPhoneCall",
                BuildOwnerCallMessage(
                    request.FinalDurationMin.HasValue
                        ? $"Termin je potvrđen telefonom. Trajanje je postavljeno na {request.FinalDurationMin.Value} minuta."
                        : "Termin je potvrđen telefonom.",
                    note),
                null,
                $"Status={appointment.Status};Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o};FinalDurationMin={request.FinalDurationMin}",
                cancellationToken);

            return Ok(new AppointmentActionResponse
            {
                AppointmentId = appointment.Id,
                AppointmentStatus = appointment.Status.ToString(),
                Action = "ConfirmedByPhoneCall",
                StartAtUtc = appointment.StartAtUtc,
                EndAtUtc = appointment.EndAtUtc,
                Message = request.FinalDurationMin.HasValue
                    ? $"Termin je potvrđen telefonom. Trajanje je postavljeno na {request.FinalDurationMin.Value} minuta."
                    : "Termin je potvrđen telefonom."
            });
        }

        await AddAuditLogAsync(
            appointment.Id,
            "ConfirmedByPhoneCall",
            BuildOwnerCallMessage(
                request.FinalDurationMin.HasValue
                    ? $"Klijent je potvrdio termin telefonom. Trajanje je postavljeno na {request.FinalDurationMin.Value} minuta."
                    : "Klijent je potvrdio termin telefonom.",
                note),
            null,
            $"Status={appointment.Status};Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o};FinalDurationMin={request.FinalDurationMin}",
            cancellationToken);

        return Ok(new AppointmentActionResponse
        {
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status.ToString(),
            Action = "ConfirmedByPhoneCall",
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            Message = request.FinalDurationMin.HasValue
                ? $"Termin je potvrđen telefonom. Trajanje je postavljeno na {request.FinalDurationMin.Value} minuta."
                : "Termin je potvrđen telefonom."
        });
    }

    [HttpPost("mark-called-rejected")]
    public async Task<ActionResult<AppointmentActionResponse>> MarkCalledRejected(
    [FromBody] MarkAppointmentCallActionRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        Appointment? appointment;

        try
        {
            appointment = await GetActiveAppointmentForOwnerCallActionAsync(
                request.AppointmentId,
                "Odbijanje pozivom više nije moguće jer termin nije aktivan.",
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        if (appointment is null)
            return NotFound("Termin ne postoji.");

        if (appointment.Status != AppointmentStatus.PendingApproval)
            return BadRequest("Odbijanje pozivom je moguće samo dok termin još čeka potvrdu.");

        var now = DateTime.UtcNow;
        var note = request.Note?.Trim();

        await RejectPendingAppointmentAsync(
            appointment,
            note,
            cancellationToken);

        await AddAuditLogAsync(
            appointment.Id,
            "RejectedByPhoneCall",
            BuildOwnerCallMessage(
"Termin je odbijen nakon razgovora sa klijentom.",
    note),
            null,
            $"Status={appointment.Status}",
            cancellationToken);

        return Ok(new AppointmentActionResponse
        {
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status.ToString(),
            Action = "RejectedByPhoneCall",
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            Message = "Termin je odbijen nakon razgovora sa klijentom."
        });
    }

    [HttpPost("mark-called-reschedule-needed")]
    public async Task<ActionResult<AppointmentActionResponse>> MarkCalledRescheduleNeeded(
    [FromBody] MarkAppointmentCallActionRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        Appointment? appointment;

        try
        {
            appointment = await GetActiveAppointmentForOwnerCallActionAsync(
                request.AppointmentId,
                "Ova radnja više nije moguća jer termin nije aktivan.",
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        if (appointment is null)
            return NotFound("Termin ne postoji.");

        var note = request.Note?.Trim();

        await AddAuditLogAsync(
            appointment.Id,
            "RescheduleNeededAfterCall",
            BuildOwnerCallMessage(
"Potrebno je dogovoriti novi termin.",
    note),
            null,
            $"Status={appointment.Status};Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o}",
            cancellationToken);

        return Ok(new AppointmentActionResponse
        {
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status.ToString(),
            Action = "RescheduleNeededAfterCall",
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            Message = "Potrebno je dogovoriti novi termin."
        });
    }

    [HttpPost("mark-call-attempt-scheduled")]
    public async Task<ActionResult<AppointmentActionResponse>> MarkCallAttemptScheduled(
     [FromBody] ScheduleCallAttemptRequest request,
     CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        if (request.ScheduledAtUtc <= DateTime.UtcNow)
            return BadRequest("Vreme novog poziva mora biti u budućnosti.");

        Appointment? appointment;

        try
        {
            appointment = await GetActiveAppointmentForOwnerCallActionAsync(
                request.AppointmentId,
                "Novi pokušaj poziva ne može da se zakaže jer termin nije aktivan.",
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        if (appointment is null)
            return NotFound("Termin ne postoji.");

        var note = request.Note?.Trim();

        await AddAuditLogAsync(
            appointment.Id,
            "CallAttemptScheduled",
            BuildOwnerCallMessage(
                "Zakazan je novi poziv klijentu.",
                note),
            null,
            $"ScheduledAtUtc={request.ScheduledAtUtc:o};Status={appointment.Status};Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o}",
            cancellationToken);

        return Ok(new AppointmentActionResponse
        {
            AppointmentId = appointment.Id,
            AppointmentStatus = appointment.Status.ToString(),
            Action = "CallAttemptScheduled",
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            ScheduledAtUtc = request.ScheduledAtUtc,
            Message = "Zakazan je novi poziv klijentu."
        });
    }

    [HttpGet("audit-log")]
    public async Task<ActionResult<List<AppointmentAuditLogItemResponse>>> GetAuditLog(
    [FromQuery] long appointmentId,
    CancellationToken cancellationToken)
    {
        var items = await _dbContext.AppointmentAuditLogs
            .AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId)
            .OrderBy(x => x.CreatedAtUtc)
.Select(x => new AppointmentAuditLogItemResponse
{
    Id = x.Id,
    AppointmentId = x.AppointmentId,
    ActionType = x.ActionType,
    Message = x.Message,
    OldValuesJson = x.OldValuesJson,
    NewValuesJson = x.NewValuesJson,
    CreatedAtUtc = x.CreatedAtUtc
})
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("inbox")]
    public async Task<ActionResult<List<AppointmentInboxItemDto>>> GetInbox(
    [FromQuery] long businessId,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

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
        var ownerActionTypes = new[]
{
    "CallCustomerMarked",
    "CustomerCallNoAnswer",
    "ConfirmedByPhoneCall",
    "RescheduleNeededAfterCall",
    "RejectedByPhoneCall",
    "CallAttemptScheduled"
};

        var latestOwnerActions = await _dbContext.AppointmentAuditLogs
            .AsNoTracking()
            .Where(x =>
                appointmentIds.Contains(x.AppointmentId) &&
                ownerActionTypes.Contains(x.ActionType))
            .GroupBy(x => x.AppointmentId)
            .Select(g => g
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new
                {
                    x.AppointmentId,
                    x.ActionType,
                    x.CreatedAtUtc,
                    x.NewValuesJson
                })
                .First())
            .ToListAsync(cancellationToken);

        var latestOwnerActionByAppointment = latestOwnerActions
            .ToDictionary(x => x.AppointmentId, x => x);

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

            latestOwnerActionByAppointment.TryGetValue(appointment.Id, out var latestOwnerAction);
            var ownerWorkflowState = GetOwnerWorkflowState(
    appointment,
    pendingChange,
    latestOwnerAction?.ActionType);
            var scheduledCallAttemptAtUtc = TryExtractScheduledAtUtc(latestOwnerAction?.NewValuesJson);

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
                ExpiresAtUtc = pendingChange?.ExpiresAtUtc,
                LastOwnerAction = latestOwnerAction?.ActionType,
                LastOwnerActionAtUtc = latestOwnerAction?.CreatedAtUtc,
                LastOwnerActionLabel = GetOwnerActionLabel(latestOwnerAction?.ActionType),
                RequiresOwnerFollowUp = RequiresOwnerFollowUp(
    appointment,
    pendingChange,
    latestOwnerAction?.ActionType),
                FollowUpHint = GetFollowUpHint(
    appointment,
    pendingChange,
    latestOwnerAction?.ActionType,
    scheduledCallAttemptAtUtc),

                OwnerWorkflowState = ownerWorkflowState,
                OwnerWorkflowLabel = GetOwnerWorkflowLabel(ownerWorkflowState),
                ScheduledCallAttemptAtUtc = scheduledCallAttemptAtUtc
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
    private static DateTime GetNewBookingRequestExpirationUtc(DateTime nowUtc)
    {
        return nowUtc.AddHours(24);
    }

    private static DateTime GetCounterProposalExpirationUtc(DateTime nowUtc)
    {
        return nowUtc.AddHours(12);
    }

    private static DateTime GetDelayProposalExpirationUtc(DateTime nowUtc)
    {
        return nowUtc.AddMinutes(15);
    }
    private async Task<OwnerCreateAvailabilityResult> IsSpecificTimeAvailableAsync(
      long businessId,
      long staffMemberId,
      DateTime startAtUtc,
      DateTime endAtUtc,
      long? ignoreAppointmentId,
      bool ignoreWorkingHours,
      bool ignoreTimeOffBlocks,
      bool ignoreAppointmentConflicts,
      CancellationToken cancellationToken)
    {
        var result = new OwnerCreateAvailabilityResult
        {
            WasAvailableByRules = true,
            IsAvailable = true,
            Message = "Izabrani termin je dostupan.",
            ReasonCode = "available"
        };

        var targetDate = startAtUtc.Date;

        if (endAtUtc.Date != targetDate)
        {
            result.IsAvailable = false;
            result.WasAvailableByRules = false;
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
            {
                result.HasBusinessHoursViolation = true;
            }

            var staffHours = await _dbContext.StaffWorkingHours
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.StaffMemberId == staffMemberId && x.DayOfWeek == dayOfWeek,
                    cancellationToken);

            if (staffHours is null || staffHours.IsClosed)
            {
                result.HasStaffHoursViolation = true;
            }

            if (!result.HasBusinessHoursViolation && !result.HasStaffHoursViolation)
            {
                var effectiveStart = businessHours!.StartTime > staffHours!.StartTime
                    ? businessHours.StartTime
                    : staffHours.StartTime;

                var effectiveEnd = businessHours.EndTime < staffHours.EndTime
                    ? businessHours.EndTime
                    : staffHours.EndTime;

                if (effectiveEnd <= effectiveStart)
                {
                    result.HasBusinessHoursViolation = true;
                    result.HasStaffHoursViolation = true;
                }
                else
                {
                    var allowedStartUtc = targetDate.Add(effectiveStart);
                    var allowedEndUtc = targetDate.Add(effectiveEnd);

                    if (startAtUtc < allowedStartUtc)
                        result.HasBusinessHoursViolation = true;

                    if (endAtUtc > allowedEndUtc)
                        result.HasStaffHoursViolation = true;
                }
            }
        }

        if (!ignoreAppointmentConflicts)
        {
            result.HasAppointmentConflict = await _dbContext.Appointments
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

        if (!ignoreTimeOffBlocks)
        {
            result.HasTimeOffConflict = await _dbContext.TimeOffBlocks
                .AsNoTracking()
                .AnyAsync(x =>
                    x.BusinessId == businessId &&
                    (x.StaffMemberId == null || x.StaffMemberId == staffMemberId) &&
                    startAtUtc < x.EndAtUtc &&
                    endAtUtc > x.StartAtUtc,
                    cancellationToken);
        }

        result.WasAvailableByRules =
            !result.HasBusinessHoursViolation &&
            !result.HasStaffHoursViolation &&
            !result.HasAppointmentConflict &&
            !result.HasTimeOffConflict;

        result.IsAvailable = result.WasAvailableByRules;

        if (!result.IsAvailable)
        {
            result.Message = BuildOwnerCreateUnavailableMessage(result);
            result.ReasonCode = GetOwnerCreateReasonCode(result);
            result.ReasonCodes = GetOwnerCreateReasonCodes(result);
        }

        return result;
    }
    private static string BuildOwnerCreateAuditMessage(
        bool wasAvailableByRules,
        bool bypassedSlotGrid,
        bool bypassedWorkingHours,
        bool bypassedTimeOffBlocks,
        bool bypassedAppointmentConflicts)
    {
        if (wasAvailableByRules)
            return "Preduzetnik je ručno uneo i potvrdio termin.";

        var parts = new List<string>();

        if (bypassedSlotGrid)
            parts.Add("rasporeda termina");

        if (bypassedWorkingHours)
            parts.Add("radnog vremena");

        if (bypassedTimeOffBlocks)
            parts.Add("označenog perioda nedostupnosti");

        if (bypassedAppointmentConflicts)
            parts.Add("konflikta sa drugim terminima");

        if (parts.Count == 0)
            return "Termin je ručno unet i potvrđen uz posebno odobrenje.";

        return $"Termin je ručno unet i potvrđen uz odstupanje od: {string.Join(", ", parts)}.";
    }
    private static string BuildOwnerCreateResponseMessage(
        bool wasAvailableByRules,
        bool bypassedSlotGrid,
        bool bypassedWorkingHours,
        bool bypassedTimeOffBlocks,
        bool bypassedAppointmentConflicts)
    {
        if (wasAvailableByRules)
            return "Termin je uspešno sačuvan.";

        var parts = new List<string>();

        if (bypassedSlotGrid)
            parts.Add("van rasporeda termina");

        if (bypassedWorkingHours)
            parts.Add("van radnog vremena");

        if (bypassedTimeOffBlocks)
            parts.Add("preko zauzetog perioda");

        if (bypassedAppointmentConflicts)
            parts.Add("preko drugog termina");

        if (parts.Count == 0)
            return "Termin je sačuvan.";

        return $"Termin je sačuvan iako je bio {string.Join(", ", parts)}.";
    }
    private static string GetOwnerCreateCreationMode(
        bool bypassedSlotGrid,
        bool bypassedWorkingHours,
        bool bypassedTimeOffBlocks,
        bool bypassedAppointmentConflicts)
    {
        if (!bypassedSlotGrid && !bypassedWorkingHours && !bypassedTimeOffBlocks && !bypassedAppointmentConflicts)
            return "normal";

        return "manual_override";
    }
    private static string GetOwnerCreateCreationModeLabel(
    string creationMode)
    {
        return creationMode switch
        {
            "normal" => "Termin je unet regularno",
            "manual_override" => "Termin je unet uz ručno odobrenje",
            _ => "Termin je unet"
        };
    }
    private static List<string> GetOwnerCreateAppliedOverrides(
        bool bypassedSlotGrid,
        bool bypassedWorkingHours,
        bool bypassedTimeOffBlocks,
        bool bypassedAppointmentConflicts)
    {
        var appliedOverrides = new List<string>();

        if (bypassedSlotGrid)
            appliedOverrides.Add("slot_grid");

        if (bypassedWorkingHours)
            appliedOverrides.Add("working_hours");

        if (bypassedTimeOffBlocks)
            appliedOverrides.Add("time_off_blocks");

        if (bypassedAppointmentConflicts)
            appliedOverrides.Add("appointment_conflicts");

        return appliedOverrides;
    }
    private static List<string> GetOwnerCreateAppliedOverrideLabels(
        bool bypassedSlotGrid,
        bool bypassedWorkingHours,
        bool bypassedTimeOffBlocks,
        bool bypassedAppointmentConflicts)
    {
        var labels = new List<string>();

        if (bypassedSlotGrid)
            labels.Add("Raspored termina");

        if (bypassedWorkingHours)
            labels.Add("Radno vreme");

        if (bypassedTimeOffBlocks)
            labels.Add("Nedostupnost u tom periodu");

        if (bypassedAppointmentConflicts)
            labels.Add("Preklapanje sa drugim terminom");

        return labels;
    }

    private async Task<OwnerCreateAvailabilityResult> IsOwnerCreateTimeAvailableAsync(
     CreateOwnerAppointmentRequest request,
     DateTime endAtUtc,
     CancellationToken cancellationToken)
    {
        if (!request.PrimaryStaffMemberId.HasValue)
        {
            return new OwnerCreateAvailabilityResult
            {
                IsAvailable = false,
                Message = "Potrebno je izabrati zaposlenog.",
                ReasonCode = "staff_required",
                ReasonCodes = new List<string> { "staff_required" }
            };
        }

        var useLegacyMasterOverride = request.IgnoreAvailabilityRules;

        var effectiveIgnoreWorkingHours =
            useLegacyMasterOverride || request.IgnoreWorkingHours;

        var effectiveIgnoreTimeOffBlocks =
            useLegacyMasterOverride || request.IgnoreTimeOffBlocks;

        var effectiveIgnoreAppointmentConflicts =
            useLegacyMasterOverride || request.IgnoreAppointmentConflicts;

        return await IsSpecificTimeAvailableAsync(
            request.BusinessId,
            request.PrimaryStaffMemberId.Value,
            request.StartAtUtc,
            endAtUtc,
            null,
            effectiveIgnoreWorkingHours,
            effectiveIgnoreTimeOffBlocks,
            effectiveIgnoreAppointmentConflicts,
            cancellationToken);
    }
    private static string BuildOwnerCreateUnavailableMessage(OwnerCreateAvailabilityResult result)
    {
        var reasons = new List<string>();

        if (result.HasSlotGridViolation)
            reasons.Add("van rasporeda termina");

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

        if (reasons.Count == 0)
            return "Izabrani termin trenutno nije dostupan.";

        return $"Izabrani termin nije dostupan jer je {string.Join(", ", reasons)}.";
    }

    private static string GetOwnerCreateReasonCode(OwnerCreateAvailabilityResult result)
    {
        if (result.HasSlotGridViolation)
            return "outside_slot_grid";

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

        return "not_available";
    }
    private static List<string> GetOwnerCreateReasonCodes(OwnerCreateAvailabilityResult result)
    {
        var codes = new List<string>();

        if (result.HasSlotGridViolation)
            codes.Add("outside_slot_grid");

        if (result.HasBusinessHoursViolation)
            codes.Add("outside_business_hours");

        if (result.HasStaffHoursViolation)
            codes.Add("outside_staff_hours");

        if (result.HasTimeOffConflict)
            codes.Add("timeoff_conflict");

        if (result.HasAppointmentConflict)
            codes.Add("appointment_conflict");

        if (codes.Count == 0 && !result.IsAvailable)
            codes.Add("not_available");

        return codes;
    }

    private async Task<bool> IsStartAlignedToBusinessSlotGridAsync(
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

        var targetDate = DateTime.SpecifyKind(startAtUtc.Date, DateTimeKind.Utc);

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
            return false;

        var staffHours = await _dbContext.StaffWorkingHours
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.StaffMemberId == staffMemberId && x.DayOfWeek == dayOfWeek,
                cancellationToken);

        if (staffHours is null || staffHours.IsClosed)
            return false;

        var effectiveStart = businessHours.StartTime > staffHours.StartTime
            ? businessHours.StartTime
            : staffHours.StartTime;

        var effectiveEnd = businessHours.EndTime < staffHours.EndTime
            ? businessHours.EndTime
            : staffHours.EndTime;

        if (effectiveEnd <= effectiveStart)
            return false;

        var dayStartUtc = targetDate.Add(effectiveStart);
        var dayEndUtc = targetDate.Add(effectiveEnd);

        if (startAtUtc < dayStartUtc || startAtUtc >= dayEndUtc)
            return false;

        var slotStepMinutes = business.SlotIntervalMin > 0
            ? business.SlotIntervalMin
            : 30;

        var totalMinutesFromStart = (startAtUtc - dayStartUtc).TotalMinutes;

        if (totalMinutesFromStart < 0)
            return false;

        return totalMinutesFromStart % slotStepMinutes == 0;
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
    private static string? GetOwnerActionLabel(string? actionType)
    {
        return actionType switch
        {
            "CallCustomerMarked" => "Potrebno je pozvati klijenta",
            "CustomerCallNoAnswer" => "Klijent se nije javio",
            "ConfirmedByPhoneCall" => "Potvrđeno telefonom",
            "RejectedByPhoneCall" => "Odbijeno telefonom",
            "RescheduleNeededAfterCall" => "Potrebno je dogovoriti novi termin",
            "CallAttemptScheduled" => "Zakazan novi pokušaj poziva",
            _ => null
        };
    }

    private static bool RequiresOwnerFollowUp(
    Appointment appointment,
    AppointmentChangeRequest? pendingChange,
    string? lastOwnerAction)
    {
        if (appointment.Status == AppointmentStatus.PendingApproval)
            return true;

        if (pendingChange is not null)
            return true;

        return lastOwnerAction switch
        {
            "CallCustomerMarked" => true,
            "CustomerCallNoAnswer" => true,
            "RescheduleNeededAfterCall" => true,
            "ConfirmedByPhoneCall" => false,
            "RejectedByPhoneCall" => false,
            "CallAttemptScheduled" => true,
            _ => false
        };
    }
    private static string? GetFollowUpHint(
    Appointment appointment,
    AppointmentChangeRequest? pendingChange,
    string? lastOwnerAction,
    DateTime? scheduledCallAttemptAtUtc)
    {
        if (pendingChange is not null)
        {
            return pendingChange.RequestType switch
            {
                AppointmentChangeRequestType.NewBookingRequest => "Potrebno je potvrditi termin",
                AppointmentChangeRequestType.CounterProposal => "Čeka se odgovor na novi termin",
                AppointmentChangeRequestType.DelayProposal => "Čeka se odgovor na pomeranje termina",
                _ => "Potrebno je dalje postupanje"
            };
        }

        if (appointment.Status == AppointmentStatus.PendingApproval)
            return "Potrebno je potvrditi termin";

        return lastOwnerAction switch
        {
            "CallCustomerMarked" => "Potrebno je pozvati klijenta",
            "CustomerCallNoAnswer" => "Potrebno je pokušati ponovo",
            "RescheduleNeededAfterCall" => "Potrebno je dogovoriti novi termin",
            "CallAttemptScheduled" => scheduledCallAttemptAtUtc.HasValue
                ? $"Potrebno je pozvati klijenta u {scheduledCallAttemptAtUtc.Value:HH:mm}"
                : "Potrebno je pozvati klijenta u zakazano vreme",
            "ConfirmedByPhoneCall" => "Nije potrebna dalja akcija",
            "RejectedByPhoneCall" => "Nije potrebna dalja akcija",
            _ => null
        };
    }
    private static string? GetOwnerWorkflowState(
    Appointment appointment,
    AppointmentChangeRequest? pendingChange,
    string? lastOwnerAction)
    {
        if (pendingChange is not null)
        {
            return pendingChange.RequestType switch
            {
                AppointmentChangeRequestType.NewBookingRequest => "pending_business_approval",
                AppointmentChangeRequestType.CounterProposal => "waiting_customer_for_counter_proposal",
                AppointmentChangeRequestType.DelayProposal => "waiting_customer_for_delay_proposal",
                AppointmentChangeRequestType.RescheduleRequest => "reschedule_requested",
                _ => "pending_change_request"
            };
        }

        if (appointment.Status == AppointmentStatus.PendingApproval)
            return "pending_business_approval";

        return lastOwnerAction switch
        {
            "CallCustomerMarked" => "call_customer",
            "CustomerCallNoAnswer" => "call_no_answer",
            "RescheduleNeededAfterCall" => "reschedule_needed_after_call",
            "ConfirmedByPhoneCall" => "confirmed_by_phone",
            "RejectedByPhoneCall" => "rejected_by_phone",
            _ => null
        };
    }
    private static string? GetOwnerWorkflowLabel(string? ownerWorkflowState)
    {
        return ownerWorkflowState switch
        {
            "pending_business_approval" => "Potrebno je potvrditi termin",
            "waiting_customer_for_counter_proposal" => "Čeka se odgovor klijenta na novi termin",
            "waiting_customer_for_delay_proposal" => "Čeka se odgovor klijenta na pomeranje",
            "reschedule_requested" => "Traži se promena termina",
            "pending_change_request" => "Postoji aktivan zahtev za izmenu",
            "call_customer" => "Potrebno je pozvati klijenta",
            "call_no_answer" => "Klijent se nije javio",
            "reschedule_needed_after_call" => "Potrebno je dogovoriti novi termin",
            "confirmed_by_phone" => "Potvrđeno telefonom",
            "rejected_by_phone" => "Odbijeno telefonom",
            "call_attempt_scheduled" => "Zakazan je novi poziv",
            _ => null
        };
    }
    private static DateTime? TryExtractScheduledAtUtc(string? newValuesJson)
    {
        if (string.IsNullOrWhiteSpace(newValuesJson))
            return null;

        const string prefix = "ScheduledAtUtc=";
        var startIndex = newValuesJson.IndexOf(prefix, StringComparison.Ordinal);

        if (startIndex < 0)
            return null;

        startIndex += prefix.Length;

        var endIndex = newValuesJson.IndexOf(';', startIndex);
        var value = endIndex >= 0
            ? newValuesJson[startIndex..endIndex]
            : newValuesJson[startIndex..];

        if (DateTime.TryParse(
            value,
            null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out var scheduledAtUtc))
        {
            return scheduledAtUtc;
        }

        return null;
    }

    private static bool IsInactiveForOwnerCallAction(AppointmentStatus status)
    {
        return status == AppointmentStatus.Rejected ||
               status == AppointmentStatus.Cancelled ||
               status == AppointmentStatus.Completed ||
               status == AppointmentStatus.NoShow;
    }

    private static string BuildOwnerCallMessage(string defaultMessage, string? note)
    {
        return string.IsNullOrWhiteSpace(note)
            ? defaultMessage
            : $"{defaultMessage} Dodatna napomena: {note.Trim()}";
    }

    private async Task<Appointment?> GetActiveAppointmentForOwnerCallActionAsync(
        long appointmentId,
        string inactiveErrorMessage,
        CancellationToken cancellationToken)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == appointmentId, cancellationToken);

        if (appointment is null)
            return null;

        if (IsInactiveForOwnerCallAction(appointment.Status))
            throw new InvalidOperationException(inactiveErrorMessage);

        return appointment;
    }
    private async Task<AppointmentChangeRequest?> GetLatestPendingChangeRequestAsync(
    long appointmentId,
    CancellationToken cancellationToken)
    {
        return await _dbContext.AppointmentChangeRequests
            .Where(x =>
                x.AppointmentId == appointmentId &&
                x.Status == AppointmentChangeRequestStatus.Pending)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
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
        public string Message { get; set; } = string.Empty;
        public string ReasonCode { get; set; } = string.Empty;
        public List<string> ReasonCodes { get; set; } = new();
    }

    private async Task ApprovePendingAppointmentAsync(
        Appointment appointment,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        appointment.Status = AppointmentStatus.Confirmed;
        appointment.UpdatedAtUtc = now;

        var pendingRequest = await GetLatestPendingChangeRequestAsync(
            appointment.Id,
            cancellationToken);

        if (pendingRequest is not null)
        {
            pendingRequest.Status = AppointmentChangeRequestStatus.Accepted;
            pendingRequest.RespondedAtUtc = now;
            pendingRequest.UpdatedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RejectPendingAppointmentAsync(
        Appointment appointment,
        string? reason,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        appointment.Status = AppointmentStatus.Rejected;
        appointment.UpdatedAtUtc = now;

        var pendingRequest = await GetLatestPendingChangeRequestAsync(
            appointment.Id,
            cancellationToken);

        if (pendingRequest is not null)
        {
            pendingRequest.Status = AppointmentChangeRequestStatus.Rejected;
            pendingRequest.Reason = reason?.Trim();
            pendingRequest.RespondedAtUtc = now;
            pendingRequest.UpdatedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

}