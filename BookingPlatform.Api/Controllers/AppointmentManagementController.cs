using BookingPlatform.Api.Helpers;
using BookingPlatform.Api.Services;
using BookingPlatform.Contracts.Appointments;
using BookingPlatform.Contracts.Common;
using BookingPlatform.Domain.Appointments;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookingPlatform.Domain.Auth;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[Route("api/Appointments")]
public sealed class AppointmentManagementController : ApiControllerBase
{
    private readonly IAppointmentSchedulingService _appointmentSchedulingService;
    private readonly IAppointmentWorkflowService _appointmentWorkflowService;
    private readonly IChatSystemMessageService _chatSystemMessageService;

    public AppointmentManagementController(
        BookingDbContext dbContext,
        IAppointmentSchedulingService appointmentSchedulingService,
        IAppointmentWorkflowService appointmentWorkflowService,
        IChatSystemMessageService chatSystemMessageService) : base(dbContext)
    {
        _appointmentSchedulingService = appointmentSchedulingService;
        _appointmentWorkflowService = appointmentWorkflowService;
        _chatSystemMessageService = chatSystemMessageService;
    }



    [HttpGet]
    [ProducesResponseType(typeof(List<AppointmentListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<AppointmentListItemResponse>>> GetAll(
     [FromQuery] long? businessId,
     [FromQuery] long? businessCustomerId,
     CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        if (!businessId.HasValue)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId.Value, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var query = DbContext.Appointments
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId.Value);

        if (businessCustomerId.HasValue)
        {
            var customerExists = await DbContext.BusinessCustomers
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == businessCustomerId.Value &&
                         x.BusinessId == businessId.Value,
                    cancellationToken);

            if (!customerExists)
                return BadRequest("Izabrani klijent ne pripada ovom biznisu.");

            query = query.Where(x => x.BusinessCustomerId == businessCustomerId.Value);
        }

        var appointments = await query
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync(cancellationToken);

        var resourceIds = appointments
            .Where(x => x.ResourceId.HasValue)
            .Select(x => x.ResourceId!.Value)
            .Distinct()
            .ToList();

        var resources = await DbContext.Resources
            .AsNoTracking()
            .Where(x => resourceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var serviceIds = appointments
    .Select(x => x.ServiceId)
    .Distinct()
    .ToList();

        var services = await DbContext.Services
            .AsNoTracking()
            .Where(x => serviceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var staffIds = appointments
            .Where(x => x.PrimaryStaffMemberId.HasValue)
            .Select(x => x.PrimaryStaffMemberId!.Value)
            .Distinct()
            .ToList();

        var staff = await DbContext.StaffMembers
            .AsNoTracking()
            .Where(x => staffIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var items = appointments
            .Select(x =>
            {
                string? resourceName = null;
                if (x.ResourceId.HasValue && resources.TryGetValue(x.ResourceId.Value, out var foundResourceName))
                    resourceName = foundResourceName;

                string? serviceName = null;
                if (services.TryGetValue(x.ServiceId, out var foundServiceName))
                    serviceName = foundServiceName;

                string? staffDisplayName = null;
                if (x.PrimaryStaffMemberId.HasValue &&
                    staff.TryGetValue(x.PrimaryStaffMemberId.Value, out var foundStaffName))
                {
                    staffDisplayName = foundStaffName;
                }

                return new AppointmentListItemResponse
                {
                    Id = x.Id,
                    BusinessId = x.BusinessId,
                    ServiceId = x.ServiceId,
                    ServiceName = serviceName,
                    PrimaryStaffMemberId = x.PrimaryStaffMemberId,
                    StaffDisplayName = staffDisplayName,
                    ResourceId = x.ResourceId,
                    ResourceName = resourceName,
                    CustomerName = x.CustomerName,
                    CustomerPhone = x.CustomerPhone,
                    BusinessCustomerId = x.BusinessCustomerId,
                    Status = x.Status.ToString(),
                    StartAtUtc = x.StartAtUtc,
                    EndAtUtc = x.EndAtUtc,
                    Notes = x.Notes,
                    CreatedAtUtc = x.CreatedAtUtc,
                    UpdatedAtUtc = x.UpdatedAtUtc
                };
            })
            .ToList();

        return Ok(items);
    }


    [HttpPost("owner-create")]
    [ProducesResponseType(typeof(OwnerCreateAppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OwnerCreateAppointmentResponse>> CreateOwnerAppointment(
    [FromBody] CreateOwnerAppointmentRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var accessResult = await EnsureBusinessWriteAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var service = await DbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.ServiceId && x.BusinessId == request.BusinessId,
                cancellationToken);

        if (service is null)
            return BadRequest("Izabrana usluga ne postoji.");

        var staffServiceValidationError = await StaffServiceValidationHelper.ValidateStaffCanPerformServiceAsync(
            DbContext,
            request.BusinessId,
            request.ServiceId,
            request.PrimaryStaffMemberId,
            cancellationToken);

        if (staffServiceValidationError is not null)
            return BadRequest(staffServiceValidationError);

        var staffResourceValidationError = await StaffResourceValidationHelper.ValidateStaffCanUseResourceAsync(
            DbContext,
            request.BusinessId,
            request.PrimaryStaffMemberId,
            request.ResourceId,
            cancellationToken);

        if (staffResourceValidationError is not null)
            return BadRequest(staffResourceValidationError);

        if (string.IsNullOrWhiteSpace(request.CustomerName))
            return BadRequest("Unesite ime klijenta.");

        if (string.IsNullOrWhiteSpace(request.CustomerPhone))
            return BadRequest("Unesite broj telefona klijenta.");

        if (request.BusinessCustomerId.HasValue)
        {
            var businessCustomerExists = await DbContext.BusinessCustomers
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == request.BusinessCustomerId.Value &&
                         x.BusinessId == request.BusinessId &&
                         x.IsActive,
                    cancellationToken);

            if (!businessCustomerExists)
                return BadRequest("Izabrani klijent ne postoji ili ne pripada ovom biznisu.");
        }

        if (!request.PrimaryStaffMemberId.HasValue)
            return BadRequest("Potrebno je izabrati zaposlenog.");

        var resourceValidationError = await ValidateServiceResourceSelectionAsync(
            request.BusinessId,
            request.ServiceId,
            request.ResourceId,
            cancellationToken);

        if (resourceValidationError is not null)
            return BadRequest(resourceValidationError);

        var defaultDurationMin = await _appointmentSchedulingService.GetTotalServiceDurationAsync(
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

        Console.WriteLine("========== OWNER CREATE SLOT GRID DEBUG ==========");
        Console.WriteLine($"Request.StartAtUtc={request.StartAtUtc:O} Kind={request.StartAtUtc.Kind}");
        Console.WriteLine($"startAtUtc={startAtUtc:O} Kind={startAtUtc.Kind}");
        Console.WriteLine($"endAtUtc={endAtUtc:O} Kind={endAtUtc.Kind}");
        Console.WriteLine($"BusinessId={request.BusinessId}");
        Console.WriteLine($"StaffMemberId={request.PrimaryStaffMemberId.Value}");
        Console.WriteLine("Calling IsStartAlignedToBusinessSlotGridAsync...");
        Console.WriteLine("==================================================");

        var isAlignedToSlotGrid = await _appointmentSchedulingService.IsStartAlignedToBusinessSlotGridAsync(
            request.BusinessId,
            request.PrimaryStaffMemberId.Value,
            startAtUtc,
            cancellationToken);

        Console.WriteLine("========== OWNER CREATE SLOT GRID RESULT ==========");
        Console.WriteLine($"IsAlignedToSlotGrid={isAlignedToSlotGrid}");
        Console.WriteLine("===================================================");

        if (!isAlignedToSlotGrid)
        {
            hasSlotGridViolation = true;
        }

        var availability = await IsOwnerCreateTimeAvailableAsync(
            request,
            endAtUtc,
            cancellationToken);

        availability.HasSlotGridViolation = false;

        var bypassedSlotGrid = false;

        var bypassedWorkingHours =
            (availability.HasBusinessHoursViolation || availability.HasStaffHoursViolation) &&
            effectiveIgnoreWorkingHours;

        var bypassedTimeOffBlocks =
            availability.HasTimeOffConflict &&
            effectiveIgnoreTimeOffBlocks;

        var bypassedAppointmentConflicts =
            availability.HasAppointmentConflict &&
            effectiveIgnoreAppointmentConflicts;

        var appliedOverrides = AppointmentOwnerCreateHelper.GetOwnerCreateAppliedOverrides(
            bypassedSlotGrid,
            bypassedWorkingHours,
            bypassedTimeOffBlocks,
            bypassedAppointmentConflicts);

        var appliedOverrideLabels = AppointmentOwnerCreateHelper.GetOwnerCreateAppliedOverrideLabels(
            bypassedSlotGrid,
            bypassedWorkingHours,
            bypassedTimeOffBlocks,
            bypassedAppointmentConflicts);

        var creationMode = AppointmentOwnerCreateHelper.GetOwnerCreateCreationMode(
            bypassedSlotGrid,
            bypassedWorkingHours,
            bypassedTimeOffBlocks,
            bypassedAppointmentConflicts);

        var creationModeLabel = AppointmentOwnerCreateHelper.GetOwnerCreateCreationModeLabel(creationMode);

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
                HasResourceConflict = availability.HasResourceConflict,
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
            ResourceId = request.ResourceId,
            BusinessCustomerId = request.BusinessCustomerId,
            CustomerName = request.CustomerName.Trim(),
            CustomerPhone = request.CustomerPhone.Trim(),
            StartAtUtc = startAtUtc,
            EndAtUtc = endAtUtc,
            Status = AppointmentStatus.Confirmed,
            Notes = request.Notes?.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.Appointments.Add(appointment);
        await DbContext.SaveChangesAsync(cancellationToken);

        await _appointmentSchedulingService.CreateAppointmentStaffUsagesAsync(
            appointment.Id,
            appointment.BusinessId,
            appointment.ServiceId,
            appointment.PrimaryStaffMemberId!.Value,
            appointment.StartAtUtc,
            appointment.EndAtUtc,
            cancellationToken);

        await AddAuditLogAsync(
                    appointment.Id,
            "OwnerCreatedConfirmedAppointment",
            AppointmentOwnerCreateHelper.BuildOwnerCreateAuditMessage(
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
            ResourceId = appointment.ResourceId,
            BusinessCustomerId = appointment.BusinessCustomerId,
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
            Message = AppointmentOwnerCreateHelper.BuildOwnerCreateResponseMessage(
                availability.WasAvailableByRules,
                bypassedSlotGrid,
                bypassedWorkingHours,
                bypassedTimeOffBlocks,
                bypassedAppointmentConflicts),
            ReasonCodes = availability.ReasonCodes
        });
    }


    [HttpPost("approve")]
    [ProducesResponseType(typeof(AppointmentActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentActionResponse>> Approve(
    [FromBody] ApproveAppointmentRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);
        var accessResult = await EnsureAppointmentWriteAccessAsync(request.AppointmentId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var appointment = await DbContext.Appointments
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
                "Ovaj termin više ne čeka potvrdu.",
                "appointment_not_pending_approval"));
        }

        if (request.FinalDurationMin.HasValue)
        {
            if (request.FinalDurationMin.Value <= 0)
            {
                return BadRequest(BuildAppointmentOperationErrorResponse(
                    "Trajanje termina mora biti veće od 0 minuta.",
                    "invalid_final_duration"));
            }

            if (request.FinalDurationMin.Value % 5 != 0)
            {
                return BadRequest(BuildAppointmentOperationErrorResponse(
                    "Trajanje termina mora biti zadato u koracima od 5 minuta.",
                    "invalid_duration_step"));
            }

            if (!appointment.PrimaryStaffMemberId.HasValue)
            {
                return BadRequest(BuildAppointmentOperationErrorResponse(
                    "Za potvrdu termina je potrebno da termin ima izabranog zaposlenog.",
                    "staff_required"));
            }

            var proposedEndAtUtc = appointment.StartAtUtc.AddMinutes(request.FinalDurationMin.Value);

            var availability = await CheckAvailabilityViaServiceAsync(
                appointment.BusinessId,
                appointment.ServiceId,
                appointment.PrimaryStaffMemberId.Value,
                appointment.ResourceId,
                appointment.StartAtUtc,
                proposedEndAtUtc,
                appointment.Id,
                false,
                false,
                false,
                cancellationToken);

            if (!availability.IsAvailable)
                return BadRequest(BuildAppointmentOperationErrorResponse(availability));

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

        var pendingChangeRequest = await GetLatestPendingChangeRequestAsync(
            appointment.Id,
            cancellationToken);

        await ApprovePendingAppointmentAsync(
            appointment,
            cancellationToken);

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

        if (pendingChangeRequest is not null)
        {
            await _chatSystemMessageService.SendAppointmentApprovedToCustomerAsync(
                appointment,
                pendingChangeRequest,
                cancellationToken);
        }

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
    [ProducesResponseType(typeof(AppointmentActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentActionResponse>> Reject(
    [FromBody] RejectAppointmentRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);
        var accessResult = await EnsureAppointmentWriteAccessAsync(request.AppointmentId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var appointment = await DbContext.Appointments
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
                "Ovaj termin više ne čeka potvrdu.",
                "appointment_not_pending_approval"));
        }

        var pendingChangeRequest = await GetLatestPendingChangeRequestAsync(
            appointment.Id,
            cancellationToken);

        await RejectPendingAppointmentAsync(
            appointment,
            request.Reason,
            cancellationToken);

        if (pendingChangeRequest is not null)
        {
            await _chatSystemMessageService.SendAppointmentRejectedToCustomerAsync(
                appointment,
                pendingChangeRequest,
                cancellationToken);
        }

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
            Action = "Rejected",
            StartAtUtc = appointment.StartAtUtc,
            EndAtUtc = appointment.EndAtUtc,
            Message = "Termin je odbijen."
        });
    }

    [HttpPost("propose-time")]
    [ProducesResponseType(typeof(AppointmentChangeActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentChangeActionResponse>> ProposeTime(
    [FromBody] ProposeAppointmentTimeRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var accessResult = await EnsureAppointmentWriteAccessAsync(request.AppointmentId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var appointment = await DbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        if (!appointment.PrimaryStaffMemberId.HasValue &&
            !request.ProposedStaffMemberId.HasValue)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                "Za predlog novog termina je potrebno da termin ima izabranog zaposlenog.",
                "staff_required"));
        }

        var proposedStaffMemberId =
            request.ProposedStaffMemberId
            ?? appointment.PrimaryStaffMemberId!.Value;

        var service = await DbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == appointment.ServiceId && x.BusinessId == appointment.BusinessId,
                cancellationToken);

        if (service is null)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                "Izabrana usluga ne postoji.",
                "service_not_found"));
        }

        var staff = await DbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == proposedStaffMemberId &&
                     x.BusinessId == appointment.BusinessId &&
                     x.IsActive &&
                     x.IsBookable,
                cancellationToken);

        if (staff is null)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                "Izabrani radnik ne postoji ili nije aktivan za zakazivanje.",
                "staff_not_found"));
        }

        var staffServiceValidationError = await StaffServiceValidationHelper.ValidateStaffCanPerformServiceAsync(
            DbContext,
            appointment.BusinessId,
            appointment.ServiceId,
            proposedStaffMemberId,
            cancellationToken);

        if (staffServiceValidationError is not null)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                staffServiceValidationError,
                "staff_cannot_perform_service"));
        }

        var staffResourceValidationError = await StaffResourceValidationHelper.ValidateStaffCanUseResourceAsync(
            DbContext,
            appointment.BusinessId,
            proposedStaffMemberId,
            appointment.ResourceId,
            cancellationToken);

        if (staffResourceValidationError is not null)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                staffResourceValidationError,
                "staff_cannot_use_resource"));
        }

        var defaultDurationMin = await _appointmentSchedulingService.GetTotalServiceDurationAsync(
            service.Id,
            service.EstimatedDurationMin,
            cancellationToken);

        if (request.FinalDurationMin.HasValue)
        {
            if (request.FinalDurationMin.Value <= 0)
            {
                return BadRequest(BuildAppointmentOperationErrorResponse(
                    "Trajanje termina mora biti veće od 0 minuta.",
                    "invalid_final_duration"));
            }

            if (request.FinalDurationMin.Value % 5 != 0)
            {
                return BadRequest(BuildAppointmentOperationErrorResponse(
                    "Trajanje termina mora biti zadato u koracima od 5 minuta.",
                    "invalid_duration_step"));
            }
        }

        var effectiveDurationMin = request.FinalDurationMin ?? defaultDurationMin;
        var proposedStart = request.ProposedStartAtUtc;
        var proposedEnd = proposedStart.AddMinutes(effectiveDurationMin);

        var isAlignedToSlotGrid = await _appointmentSchedulingService.IsStartAlignedToBusinessSlotGridAsync(
            appointment.BusinessId,
            proposedStaffMemberId,
            proposedStart,
            cancellationToken);

        if (!isAlignedToSlotGrid)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                "Predloženi početak termina nije na rasporedu radnje.",
                "outside_slot_grid",
                hasSlotGridViolation: true));
        }

        var availability = await CheckAvailabilityViaServiceAsync(
            appointment.BusinessId,
            appointment.ServiceId,
            proposedStaffMemberId,
            appointment.ResourceId,
            proposedStart,
            proposedEnd,
            appointment.Id,
            false,
            false,
            false,
            cancellationToken);

        if (!availability.IsAvailable)
            return BadRequest(BuildAppointmentOperationErrorResponse(availability));

        var result = await _appointmentWorkflowService.ProposeTimeAsync(
            request.AppointmentId,
            request.ProposedStartAtUtc,
            request.FinalDurationMin,
            request.Message,
            request.ProposedStaffMemberId,
            cancellationToken);

        if (result.NotFound)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                result.ErrorMessage,
                result.ErrorReasonCode));
        }

        if (!result.Succeeded)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                result.ErrorMessage,
                result.ErrorReasonCode));
        }

        await AddAuditLogAsync(
            result.AppointmentId,
            result.IsConfirmedRescheduleFlow ? "RescheduleRequestCounterProposed" : "CounterProposalCreated",
            request.FinalDurationMin.HasValue
                ? request.Message?.Trim() ?? $"Predložen je novi termin sa trajanjem od {request.FinalDurationMin.Value} minuta."
                : request.Message?.Trim() ?? "Predložen je novi termin.",
            $"OldStart={appointment.StartAtUtc:o};OldEnd={appointment.EndAtUtc:o};OldStaffMemberId={appointment.PrimaryStaffMemberId}",
            $"ProposedStart={result.StartAtUtc:o};ProposedEnd={result.EndAtUtc:o};ProposedStaffMemberId={request.ProposedStaffMemberId};FinalDurationMin={request.FinalDurationMin};EffectiveDurationMin={result.EffectiveDurationMin}",
            cancellationToken);

        return Ok(new AppointmentChangeActionResponse
        {
            AppointmentId = result.AppointmentId,
            AppointmentStatus = result.AppointmentStatus.ToString(),
            ChangeRequestId = result.ChangeRequestId,
            ChangeRequestStatus = result.ChangeRequestStatus.ToString(),
            StartAtUtc = result.StartAtUtc,
            EndAtUtc = result.EndAtUtc,
            DurationMin = (int)(result.EndAtUtc - result.StartAtUtc).TotalMinutes,
            Message = result.Message
        });
    }


    [HttpGet("change-requests")]
    [ProducesResponseType(typeof(List<AppointmentChangeRequestItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<AppointmentChangeRequestItemResponse>>> GetChangeRequests(
     [FromQuery] long appointmentId,
     CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var accessResult = await EnsureAppointmentReadAccessAsync(appointmentId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var items = await DbContext.AppointmentChangeRequests
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

    [HttpPost("accept-reschedule-request")]
    [ProducesResponseType(typeof(AppointmentChangeActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentChangeActionResponse>> AcceptRescheduleRequest(
     [FromBody] AcceptRescheduleRequest request,
     CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var accessResult = await EnsureAppointmentWriteAccessAsync(request.AppointmentId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var appointment = await DbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        var changeRequest = await DbContext.AppointmentChangeRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.ChangeRequestId &&
                     x.AppointmentId == request.AppointmentId &&
                     x.RequestType == AppointmentChangeRequestType.RescheduleRequest,
                cancellationToken);

        if (changeRequest is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Zahtev za promenu termina ne postoji.",
                "reschedule_request_not_found"));
        }

        var scheduleValidationError = await ValidateAcceptedScheduleAsync(
            appointment,
            changeRequest.ProposedStartAtUtc,
            changeRequest.ProposedEndAtUtc,
            cancellationToken);

        if (scheduleValidationError is not null)
            return BadRequest(BuildAppointmentOperationErrorResponse(scheduleValidationError));

        var result = await _appointmentWorkflowService.AcceptRescheduleRequestAsync(
            request.AppointmentId,
            request.ChangeRequestId,
            cancellationToken);

        if (result.NotFound)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                result.ErrorMessage,
                result.ErrorReasonCode));
        }

        if (!result.Succeeded)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                result.ErrorMessage,
                result.ErrorReasonCode));
        }

        if (appointment.PrimaryStaffMemberId.HasValue)
        {
            await _appointmentSchedulingService.CreateAppointmentStaffUsagesAsync(
                appointment.Id,
                appointment.BusinessId,
                appointment.ServiceId,
                appointment.PrimaryStaffMemberId.Value,
                result.StartAtUtc,
                result.EndAtUtc,
                cancellationToken);
        }

        await AddAuditLogAsync(
                    result.AppointmentId,
            "RescheduleRequestAccepted",
            result.Message,
            $"OldStart={result.OriginalStartAtUtc:o};OldEnd={result.OriginalEndAtUtc:o}",
            $"Start={result.StartAtUtc:o};End={result.EndAtUtc:o};Status={result.AppointmentStatus}",
            cancellationToken);

        await _chatSystemMessageService.SendRescheduleRequestAcceptedToCustomerAsync(
    appointment,
    changeRequest,
    cancellationToken);

        return Ok(new AppointmentChangeActionResponse
        {
            AppointmentId = result.AppointmentId,
            AppointmentStatus = result.AppointmentStatus.ToString(),
            ChangeRequestId = result.ChangeRequestId,
            ChangeRequestStatus = result.ChangeRequestStatus.ToString(),
            StartAtUtc = result.StartAtUtc,
            EndAtUtc = result.EndAtUtc,
            DurationMin = (int)(result.EndAtUtc - result.StartAtUtc).TotalMinutes,
            Message = result.Message
        });
    }

    [HttpPost("reject-reschedule-request")]
    [ProducesResponseType(typeof(AppointmentChangeActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentChangeActionResponse>> RejectRescheduleRequest(
     [FromBody] RejectRescheduleRequest request,
     CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var accessResult = await EnsureAppointmentWriteAccessAsync(request.AppointmentId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var appointment = await DbContext.Appointments
    .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        var changeRequest = await DbContext.AppointmentChangeRequests
            .FirstOrDefaultAsync(
                x => x.Id == request.ChangeRequestId &&
                     x.AppointmentId == request.AppointmentId &&
                     x.RequestType == AppointmentChangeRequestType.RescheduleRequest,
                cancellationToken);

        if (changeRequest is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Zahtev za promenu termina ne postoji.",
                "reschedule_request_not_found"));
        }

        var result = await _appointmentWorkflowService.RejectRescheduleRequestAsync(
            request.AppointmentId,
            request.ChangeRequestId,
            request.Reason,
            cancellationToken);

        if (result.NotFound)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                result.ErrorMessage,
                result.ErrorReasonCode));
        }

        if (!result.Succeeded)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                result.ErrorMessage,
                result.ErrorReasonCode));
        }

        await AddAuditLogAsync(
            result.AppointmentId,
            "RescheduleRequestRejected",
            result.Reason ?? result.Message,
            $"ProposedStart={result.ProposedStartAtUtc:o};ProposedEnd={result.ProposedEndAtUtc:o}",
            $"Start={result.StartAtUtc:o};End={result.EndAtUtc:o};Status={result.AppointmentStatus}",
            cancellationToken);

        await _chatSystemMessageService.SendRescheduleRequestRejectedToCustomerAsync(
    appointment,
    changeRequest,
    cancellationToken);

        return Ok(new AppointmentChangeActionResponse
        {
            AppointmentId = result.AppointmentId,
            AppointmentStatus = result.AppointmentStatus.ToString(),
            ChangeRequestId = result.ChangeRequestId,
            ChangeRequestStatus = result.ChangeRequestStatus.ToString(),
            StartAtUtc = result.StartAtUtc,
            EndAtUtc = result.EndAtUtc,
            DurationMin = (int)(result.EndAtUtc - result.StartAtUtc).TotalMinutes,
            Message = result.Message
        });
    }

    [HttpGet("{appointmentId:long}/allowed-delays")]
    [ProducesResponseType(typeof(AppointmentDelayLimitResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentDelayLimitResponse>> GetAllowedDelays(
      [FromRoute] long appointmentId,
      CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var appointment = await DbContext.Appointments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == appointmentId, cancellationToken);

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        var accessResult = await EnsureAllowedDelayAccessAsync(
            appointment,
            cancellationToken);

        if (accessResult is not null)
            return accessResult;

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                "Odlaganje može da se predloži samo za potvrđen termin.",
                "appointment_not_confirmed"));
        }

        if (!appointment.PrimaryStaffMemberId.HasValue)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                "Za proveru odlaganja termin mora imati izabranog radnika.",
                "staff_required"));
        }

        var candidateDelayMinutes = new[] { 10, 15, 30, 45, 60 };
        var allowedDelayMinutes = new List<int>();

        foreach (var delayMinutes in candidateDelayMinutes)
        {
            var proposedStartAtUtc = appointment.StartAtUtc.AddMinutes(delayMinutes);
            var proposedEndAtUtc = appointment.EndAtUtc.AddMinutes(delayMinutes);

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
            {
                // Važno:
                // Kod odlaganja ne preskačemo sledeći termin.
                // Čim prvo odlaganje ne može da stane, sva veća odlaganja se više ne nude.
                break;
            }

            allowedDelayMinutes.Add(delayMinutes);
        }

        var maxDelayMinutes = allowedDelayMinutes.Count == 0
            ? 0
            : allowedDelayMinutes.Max();

        return Ok(new AppointmentDelayLimitResponse
        {
            AppointmentId = appointment.Id,
            MaxDelayMinutes = maxDelayMinutes,
            AllowedDelayMinutes = allowedDelayMinutes,
            Message = allowedDelayMinutes.Count == 0
                ? "Ovaj termin trenutno ne može da se odloži bez ulaska u drugi termin ili zauzeće."
                : $"Moguće odlaganje do {maxDelayMinutes} minuta."
        });
    }

    [HttpPost("propose-delay")]
    [ProducesResponseType(typeof(AppointmentChangeActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentChangeActionResponse>> ProposeDelay(
       [FromBody] ProposeDelayRequest request,
       CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);
        var accessResult = await EnsureAppointmentWriteAccessAsync(request.AppointmentId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var result = await _appointmentWorkflowService.ProposeDelayAsync(
            request.AppointmentId,
            request.DelayMinutes,
            request.Message,
            cancellationToken);

        if (result.NotFound)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                result.ErrorMessage,
                result.ErrorReasonCode));
        }

        if (!result.Succeeded)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                result.ErrorMessage,
                result.ErrorReasonCode,
                hasAppointmentConflict: result.HasAppointmentConflict));
        }

        await AddAuditLogAsync(
            result.AppointmentId,
            "DelayProposalCreated",
            request.Message?.Trim() ?? $"Predloženo je pomeranje termina za {request.DelayMinutes} minuta.",
            null,
            $"ProposedStart={result.StartAtUtc:o};ProposedEnd={result.EndAtUtc:o}",
            cancellationToken);

        return Ok(new AppointmentChangeActionResponse
        {
            AppointmentId = result.AppointmentId,
            AppointmentStatus = result.AppointmentStatus.ToString(),
            ChangeRequestId = result.ChangeRequestId,
            ChangeRequestStatus = result.ChangeRequestStatus.ToString(),
            StartAtUtc = result.StartAtUtc,
            EndAtUtc = result.EndAtUtc,
            DurationMin = (int)(result.EndAtUtc - result.StartAtUtc).TotalMinutes,
            Message = result.Message
        });
    }

    [HttpPost("complete")]
    [ProducesResponseType(typeof(AppointmentActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentActionResponse>> Complete(
   [FromBody] UpdateConfirmedAppointmentStatusRequest request,
   CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var accessResult = await EnsureAppointmentConfirmedStatusUpdateAccessAsync(
            request.AppointmentId,
            cancellationToken);

        if (accessResult is not null)
            return accessResult;

        var result = await _appointmentWorkflowService.CompleteConfirmedAsync(
            request.AppointmentId,
            request.Note,
            cancellationToken);

        if (result.NotFound)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                result.ErrorMessage,
                result.ErrorReasonCode));
        }

        if (!result.Succeeded)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                result.ErrorMessage,
                result.ErrorReasonCode));
        }

        await AddAuditLogAsync(
            result.AppointmentId,
            result.Action,
            BuildStatusWorkflowMessage(result.Message, result.Note),
            $"Status={AppointmentStatus.Confirmed};Start={result.StartAtUtc:o};End={result.EndAtUtc:o}",
            $"Status={result.AppointmentStatus};Start={result.StartAtUtc:o};End={result.EndAtUtc:o}",
            cancellationToken);

        return Ok(new AppointmentActionResponse
        {
            AppointmentId = result.AppointmentId,
            AppointmentStatus = result.AppointmentStatus.ToString(),
            Action = result.Action,
            StartAtUtc = result.StartAtUtc,
            EndAtUtc = result.EndAtUtc,
            Message = result.Message
        });
    }

    [HttpPost("no-show")]
    [ProducesResponseType(typeof(AppointmentActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentActionResponse>> NoShow(
     [FromBody] UpdateConfirmedAppointmentStatusRequest request,
     CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var accessResult = await EnsureAppointmentConfirmedStatusUpdateAccessAsync(
            request.AppointmentId,
            cancellationToken);

        if (accessResult is not null)
            return accessResult;

        var result = await _appointmentWorkflowService.MarkNoShowAsync(
            request.AppointmentId,
            request.Note,
            cancellationToken);

        if (result.NotFound)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                result.ErrorMessage,
                result.ErrorReasonCode));
        }

        if (!result.Succeeded)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                result.ErrorMessage,
                result.ErrorReasonCode));
        }

        await AddAuditLogAsync(
            result.AppointmentId,
            result.Action,
            BuildStatusWorkflowMessage(result.Message, result.Note),
            $"Status={AppointmentStatus.Confirmed};Start={result.StartAtUtc:o};End={result.EndAtUtc:o}",
            $"Status={result.AppointmentStatus};Start={result.StartAtUtc:o};End={result.EndAtUtc:o}",
            cancellationToken);

        return Ok(new AppointmentActionResponse
        {
            AppointmentId = result.AppointmentId,
            AppointmentStatus = result.AppointmentStatus.ToString(),
            Action = result.Action,
            StartAtUtc = result.StartAtUtc,
            EndAtUtc = result.EndAtUtc,
            Message = result.Message
        });
    }

    [HttpPost("cancel")]
    [ProducesResponseType(typeof(AppointmentActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentActionResponse>> Cancel(
      [FromBody] UpdateConfirmedAppointmentStatusRequest request,
      CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var accessResult = await EnsureAppointmentConfirmedCancelAccessAsync(
            request.AppointmentId,
            cancellationToken);

        if (accessResult is not null)
            return accessResult;

        var appointment = await DbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        var result = await _appointmentWorkflowService.CancelConfirmedAsync(
            request.AppointmentId,
            request.Note,
            cancellationToken);

        if (result.NotFound)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                result.ErrorMessage,
                result.ErrorReasonCode));
        }

        if (!result.Succeeded)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                result.ErrorMessage,
                result.ErrorReasonCode));
        }

        await AddAuditLogAsync(
            result.AppointmentId,
            result.Action,
            BuildStatusWorkflowMessage(result.Message, result.Note),
            $"Status={AppointmentStatus.Confirmed};Start={result.StartAtUtc:o};End={result.EndAtUtc:o}",
            $"Status={result.AppointmentStatus};Start={result.StartAtUtc:o};End={result.EndAtUtc:o}",
            cancellationToken);

        await _chatSystemMessageService.SendBusinessCancelledAppointmentToCustomerAsync(
            appointment,
            request.Note,
            cancellationToken);

        return Ok(new AppointmentActionResponse
        {
            AppointmentId = result.AppointmentId,
            AppointmentStatus = result.AppointmentStatus.ToString(),
            Action = result.Action,
            StartAtUtc = result.StartAtUtc,
            EndAtUtc = result.EndAtUtc,
            Message = result.Message
        });
    }

    [HttpPost("mark-call-customer")]
    [ProducesResponseType(typeof(AppointmentActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentActionResponse>> MarkCallCustomer(
        [FromBody] MarkCallCustomerRequest request,
        CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);
        var accessResult = await EnsureAppointmentCallWorkflowAccessAsync(request.AppointmentId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

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
            return BadRequest(BuildAppointmentOperationErrorResponse(
                ex.Message,
                "appointment_inactive_for_owner_action"));
        }

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

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
    [ProducesResponseType(typeof(AppointmentActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentActionResponse>> MarkNoAnswer(
    [FromBody] MarkAppointmentCallActionRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);

        var appointment = await DbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        var accessResult = await EnsureBusinessCallWorkflowAccessAsync(
            appointment.BusinessId,
            cancellationToken);

        if (accessResult is not null)
            return accessResult;

        if (IsInactiveForOwnerCallAction(appointment.Status))
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                "Ova radnja više nije moguća jer termin nije aktivan.",
                "appointment_inactive_for_owner_action"));
        }

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
    [ProducesResponseType(typeof(AppointmentActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentActionResponse>> MarkCalledConfirmed(
    [FromBody] MarkAppointmentCallActionRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);
        var accessResult = await EnsureAppointmentWriteAccessAsync(request.AppointmentId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

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
            return BadRequest(BuildAppointmentOperationErrorResponse(
                ex.Message,
                "appointment_inactive_for_owner_action"));
        }

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        if (request.FinalDurationMin.HasValue)
        {
            if (request.FinalDurationMin.Value <= 0)
            {
                return BadRequest(BuildAppointmentOperationErrorResponse(
                    "Trajanje termina mora biti veće od 0 minuta.",
                    "invalid_final_duration"));
            }

            if (request.FinalDurationMin.Value % 5 != 0)
            {
                return BadRequest(BuildAppointmentOperationErrorResponse(
                    "Trajanje termina mora biti zadato u koracima od 5 minuta.",
                    "invalid_duration_step"));
            }

            if (!appointment.PrimaryStaffMemberId.HasValue)
            {
                return BadRequest(BuildAppointmentOperationErrorResponse(
                    "Za potvrdu termina je potrebno da termin ima izabranog zaposlenog.",
                    "staff_required"));
            }

            var proposedEndAtUtc = appointment.StartAtUtc.AddMinutes(request.FinalDurationMin.Value);

            var availability = await CheckAvailabilityViaServiceAsync(
                appointment.BusinessId,
                appointment.ServiceId,
                appointment.PrimaryStaffMemberId.Value,
                appointment.ResourceId,
                appointment.StartAtUtc,
                proposedEndAtUtc,
                appointment.Id,
                false,
                false,
                false,
                cancellationToken);

            if (!availability.IsAvailable)
                return BadRequest(BuildAppointmentOperationErrorResponse(availability));

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

            if (request.FinalDurationMin.HasValue && appointment.PrimaryStaffMemberId.HasValue)
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
    [ProducesResponseType(typeof(AppointmentActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentActionResponse>> MarkCalledRejected(
     [FromBody] MarkAppointmentCallActionRequest request,
     CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);
        var accessResult = await EnsureAppointmentWriteAccessAsync(request.AppointmentId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

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
            return BadRequest(BuildAppointmentOperationErrorResponse(
                ex.Message,
                "appointment_inactive_for_owner_action"));
        }

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        if (appointment.Status != AppointmentStatus.PendingApproval)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                "Odbijanje pozivom je moguće samo dok termin još čeka potvrdu.",
                "appointment_not_pending_approval"));
        }

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
    [ProducesResponseType(typeof(AppointmentActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentActionResponse>> MarkCalledRescheduleNeeded(
    [FromBody] MarkAppointmentCallActionRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);
        var accessResult = await EnsureAppointmentCallWorkflowAccessAsync(request.AppointmentId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

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
            return BadRequest(BuildAppointmentOperationErrorResponse(
                ex.Message,
                "appointment_inactive_for_owner_action"));
        }

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

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
    [ProducesResponseType(typeof(AppointmentActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentActionResponse>> MarkCallAttemptScheduled(
    [FromBody] ScheduleCallAttemptRequest request,
    CancellationToken cancellationToken)
    {
        await ExpirePendingRequestsAsync(cancellationToken);
        var accessResult = await EnsureAppointmentCallWorkflowAccessAsync(request.AppointmentId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (request.ScheduledAtUtc <= DateTime.UtcNow)
        {
            return BadRequest(BuildAppointmentOperationErrorResponse(
                "Vreme novog poziva mora biti u budućnosti.",
                "scheduled_call_must_be_future"));
        }

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
            return BadRequest(BuildAppointmentOperationErrorResponse(
                ex.Message,
                "appointment_inactive_for_owner_action"));
        }

        if (appointment is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

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
    [ProducesResponseType(typeof(List<AppointmentAuditLogItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AppointmentOperationErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<AppointmentAuditLogItemResponse>>> GetAuditLog(
      [FromQuery] long appointmentId,
      CancellationToken cancellationToken)
    {
        var accessResult = await EnsureAppointmentReadAccessAsync(
            appointmentId,
            cancellationToken);

        if (accessResult is not null)
            return accessResult;

        var items = await DbContext.AppointmentAuditLogs
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
    [ProducesResponseType(typeof(List<AppointmentInboxItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AppointmentInboxItemDto>>> GetInbox(
    [FromQuery] long businessId,
    CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;
        await ExpirePendingRequestsAsync(cancellationToken);

        var appointments = await DbContext.Appointments
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                (x.Status == AppointmentStatus.PendingApproval || x.Status == AppointmentStatus.Confirmed))
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync(cancellationToken);

        var appointmentIds = appointments.Select(x => x.Id).ToList();

        var pendingChangeRequests = await DbContext.AppointmentChangeRequests
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

        var latestOwnerActions = await DbContext.AppointmentAuditLogs
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

        var services = await DbContext.Services
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var staff = await DbContext.StaffMembers
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var resourceIds = appointments
            .Where(x => x.ResourceId.HasValue)
            .Select(x => x.ResourceId!.Value)
            .Distinct()
            .ToList();



        var resources = await DbContext.Resources
            .AsNoTracking()
            .Where(x => resourceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

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

            string? resourceName = null;
            if (appointment.ResourceId.HasValue &&
                resources.TryGetValue(appointment.ResourceId.Value, out var foundResourceName))
            {
                resourceName = foundResourceName;
            }

            latestOwnerActionByAppointment.TryGetValue(appointment.Id, out var latestOwnerAction);
            var ownerWorkflowState = AppointmentInboxWorkflowHelper.GetOwnerWorkflowState(
                appointment,
                pendingChange,
                latestOwnerAction?.ActionType);
            var scheduledCallAttemptAtUtc = AppointmentInboxWorkflowHelper.TryExtractScheduledAtUtc(latestOwnerAction?.NewValuesJson);

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
                ResourceId = appointment.ResourceId,
                ResourceName = resourceName,
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
                LastOwnerActionLabel = AppointmentInboxWorkflowHelper.GetOwnerActionLabel(latestOwnerAction?.ActionType),
                RequiresOwnerFollowUp = AppointmentInboxWorkflowHelper.RequiresOwnerFollowUp(
                    appointment,
                    pendingChange,
                    latestOwnerAction?.ActionType),
                FollowUpHint = AppointmentInboxWorkflowHelper.GetFollowUpHint(
                    appointment,
                    pendingChange,
                    latestOwnerAction?.ActionType,
                    scheduledCallAttemptAtUtc),
                OwnerWorkflowState = ownerWorkflowState,
                OwnerWorkflowLabel = AppointmentInboxWorkflowHelper.GetOwnerWorkflowLabel(ownerWorkflowState),
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

        DbContext.AppointmentAuditLogs.Add(log);
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ExpirePendingRequestsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var expiredRequests = await DbContext.AppointmentChangeRequests
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

        var appointments = await DbContext.Appointments
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

            DbContext.AppointmentAuditLogs.Add(new AppointmentAuditLog
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

        await DbContext.SaveChangesAsync(cancellationToken);
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

    private async Task<string?> ValidateServiceResourceSelectionAsync(
    long businessId,
    long serviceId,
    long? resourceId,
    CancellationToken cancellationToken)
    {
        var serviceResourceRequirements = await DbContext.ServiceResourceRequirements
            .AsNoTracking()
            .Where(x => x.ServiceId == serviceId)
            .ToListAsync(cancellationToken);

        if (!resourceId.HasValue)
        {
            if (serviceResourceRequirements.Any(x => x.IsRequired))
                return "Za izabranu uslugu je potrebno izabrati resurs.";

            return null;
        }

        var resource = await DbContext.Resources
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

        return await CheckAvailabilityViaServiceAsync(
            request.BusinessId,
            request.ServiceId,
            request.PrimaryStaffMemberId.Value,
            request.ResourceId,
            request.StartAtUtc,
            endAtUtc,
            null,
            effectiveIgnoreWorkingHours,
            effectiveIgnoreTimeOffBlocks,
            effectiveIgnoreAppointmentConflicts,
            cancellationToken);
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
        var appointment = await DbContext.Appointments
            .FirstOrDefaultAsync(x => x.Id == appointmentId, cancellationToken);

        if (appointment is null)
            return null;

        if (IsInactiveForOwnerCallAction(appointment.Status))
            throw new InvalidOperationException(inactiveErrorMessage);

        return appointment;
    }

    private async Task<ActionResult?> EnsureAllowedDelayAccessAsync(
    Appointment appointment,
    CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
        {
            return Unauthorized(BuildAppointmentOperationErrorResponse(
                "Korisnik nije autentifikovan.",
                "unauthenticated"));
        }

        var hasBusinessAccess = await DbContext.BusinessUserMemberships
            .AsNoTracking()
            .AnyAsync(
                x => x.AppUserId == userId.Value &&
                     x.BusinessId == appointment.BusinessId &&
                     x.IsActive,
                cancellationToken);

        if (hasBusinessAccess)
            return null;

        var customerProfile = await DbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.AppUserId == userId.Value,
                cancellationToken);

        if (customerProfile is null)
            return Forbid();

        var hasCustomerAccess = await DbContext.BusinessCustomers
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == appointment.BusinessCustomerId &&
                     x.BusinessId == appointment.BusinessId &&
                     x.CustomerProfileId == customerProfile.Id &&
                     x.IsActive,
                cancellationToken);

        if (hasCustomerAccess)
            return null;

        return Forbid();
    }

    private async Task<ActionResult?> EnsureAppointmentReadAccessAsync(
        long appointmentId,
        CancellationToken cancellationToken)
    {
        var appointmentRef = await DbContext.Appointments
            .AsNoTracking()
            .Where(x => x.Id == appointmentId)
            .Select(x => new { x.Id, x.BusinessId })
            .FirstOrDefaultAsync(cancellationToken);

        if (appointmentRef is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        return await EnsureBusinessReadAccessAsync(appointmentRef.BusinessId, cancellationToken);
    }

    private async Task<ActionResult?> EnsureAppointmentWriteAccessAsync(
        long appointmentId,
        CancellationToken cancellationToken)
    {
        var appointmentRef = await DbContext.Appointments
            .AsNoTracking()
            .Where(x => x.Id == appointmentId)
            .Select(x => new { x.Id, x.BusinessId })
            .FirstOrDefaultAsync(cancellationToken);

        if (appointmentRef is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        return await EnsureBusinessWriteAccessAsync(appointmentRef.BusinessId, cancellationToken);
    }

    private static string BuildStatusWorkflowMessage(string defaultMessage, string? note)
    {
        return string.IsNullOrWhiteSpace(note)
            ? defaultMessage
            : $"{defaultMessage} Dodatna napomena: {note.Trim()}";
    }


    private async Task<ActionResult?> EnsureAppointmentConfirmedStatusUpdateAccessAsync(
        long appointmentId,
        CancellationToken cancellationToken)
    {
        var appointmentRef = await DbContext.Appointments
            .AsNoTracking()
            .Where(x => x.Id == appointmentId)
            .Select(x => new { x.Id, x.BusinessId })
            .FirstOrDefaultAsync(cancellationToken);

        if (appointmentRef is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        return await EnsureBusinessConfirmedStatusUpdateAccessAsync(
            appointmentRef.BusinessId,
            cancellationToken);
    }

    private async Task<ActionResult?> EnsureBusinessConfirmedStatusUpdateAccessAsync(
        long businessId,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
        {
            return Unauthorized(BuildAppointmentOperationErrorResponse(
                "Korisnik nije autentifikovan.",
                "unauthenticated"));
        }

        var membership = await DbContext.BusinessUserMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.AppUserId == userId.Value &&
                x.BusinessId == businessId &&
                x.IsActive,
                cancellationToken);

        if (membership is null)
            return Forbid();

        if (membership.Role is BusinessUserRole.Owner or BusinessUserRole.Manager or BusinessUserRole.Staff)
            return null;

        return Forbid();
    }

    private async Task<ActionResult?> EnsureAppointmentConfirmedCancelAccessAsync(
        long appointmentId,
        CancellationToken cancellationToken)
    {
        var appointmentRef = await DbContext.Appointments
            .AsNoTracking()
            .Where(x => x.Id == appointmentId)
            .Select(x => new { x.Id, x.BusinessId })
            .FirstOrDefaultAsync(cancellationToken);

        if (appointmentRef is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        return await EnsureBusinessConfirmedCancelAccessAsync(
            appointmentRef.BusinessId,
            cancellationToken);
    }

    private async Task<ActionResult?> EnsureBusinessConfirmedCancelAccessAsync(
        long businessId,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
        {
            return Unauthorized(BuildAppointmentOperationErrorResponse(
                "Korisnik nije autentifikovan.",
                "unauthenticated"));
        }

        var membership = await DbContext.BusinessUserMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.AppUserId == userId.Value &&
                x.BusinessId == businessId &&
                x.IsActive,
                cancellationToken);

        if (membership is null)
            return Forbid();

        if (membership.Role is BusinessUserRole.Owner or BusinessUserRole.Manager)
            return null;

        return Forbid();
    }

    private async Task<ActionResult?> EnsureAppointmentCallWorkflowAccessAsync(
    long appointmentId,
    CancellationToken cancellationToken)
    {
        var appointmentRef = await DbContext.Appointments
            .AsNoTracking()
            .Where(x => x.Id == appointmentId)
            .Select(x => new { x.Id, x.BusinessId })
            .FirstOrDefaultAsync(cancellationToken);

        if (appointmentRef is null)
        {
            return NotFound(BuildAppointmentOperationErrorResponse(
                "Termin ne postoji.",
                "appointment_not_found"));
        }

        return await EnsureBusinessCallWorkflowAccessAsync(appointmentRef.BusinessId, cancellationToken);
    }

    private async Task<ActionResult?> EnsureBusinessCallWorkflowAccessAsync(
        long businessId,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
        {
            return Unauthorized(BuildAppointmentOperationErrorResponse(
                "Korisnik nije autentifikovan.",
                "unauthenticated"));
        }

        var membership = await DbContext.BusinessUserMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.AppUserId == userId.Value &&
                x.BusinessId == businessId &&
                x.IsActive,
                cancellationToken);

        if (membership is null)
            return Forbid();

        if (membership.Role is BusinessUserRole.Owner or BusinessUserRole.Manager or BusinessUserRole.Staff)
            return null;

        return Forbid();
    }
    private async Task<AppointmentChangeRequest?> GetLatestPendingChangeRequestAsync(
    long appointmentId,
    CancellationToken cancellationToken)
    {
        return await DbContext.AppointmentChangeRequests
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
        public bool HasResourceConflict { get; set; }
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

        await DbContext.SaveChangesAsync(cancellationToken);
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

        await DbContext.SaveChangesAsync(cancellationToken);
    }

}

