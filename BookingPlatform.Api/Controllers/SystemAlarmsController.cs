using BookingPlatform.Api.Services;
using BookingPlatform.Contracts.SystemAlarms;
using BookingPlatform.Domain.SystemAlarms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SystemAlarmsController : ControllerBase
{
    private readonly ISystemAlarmService _systemAlarmService;

    public SystemAlarmsController(ISystemAlarmService systemAlarmService)
    {
        _systemAlarmService = systemAlarmService;
    }

    [HttpGet("due")]
    public async Task<ActionResult<IReadOnlyList<SystemAlarmTriggerDto>>> GetDue(
        [FromQuery] long businessId,
        CancellationToken cancellationToken)
    {
        if (businessId <= 0)
        {
            return BadRequest("BusinessId nije ispravan.");
        }

        var alarms = await _systemAlarmService.GetDueAlarmsAsync(
            businessId,
            DateTime.UtcNow,
            cancellationToken);

        var result = alarms
            .Select(MapToDto)
            .ToList();

        return Ok(result);
    }

    [HttpPost("{alarmId:long}/mark-fired")]
    public async Task<IActionResult> MarkFired(
        [FromRoute] long alarmId,
        CancellationToken cancellationToken)
    {
        if (alarmId <= 0)
        {
            return BadRequest("AlarmId nije ispravan.");
        }

        var success = await _systemAlarmService.MarkFiredAsync(
            alarmId,
            cancellationToken);

        if (!success)
        {
            return NotFound("Alarm nije pronađen.");
        }

        return NoContent();
    }

    [HttpPost("{alarmId:long}/stop")]
    public async Task<IActionResult> Stop(
        [FromRoute] long alarmId,
        CancellationToken cancellationToken)
    {
        if (alarmId <= 0)
        {
            return BadRequest("AlarmId nije ispravan.");
        }

        var success = await _systemAlarmService.StopAsync(
            alarmId,
            cancellationToken);

        if (!success)
        {
            return NotFound("Alarm nije pronađen.");
        }

        return NoContent();
    }

    [HttpPost("{alarmId:long}/snooze")]
    public async Task<IActionResult> Snooze(
        [FromRoute] long alarmId,
        [FromBody] SnoozeSystemAlarmRequest request,
        CancellationToken cancellationToken)
    {
        if (alarmId <= 0)
        {
            return BadRequest("AlarmId nije ispravan.");
        }

        if (request.Minutes is not (1 or 3 or 5 or 10 or 15))
        {
            return BadRequest("Dozvoljeno odlaganje alarma je 1, 3, 5, 10 ili 15 minuta.");
        }

        var success = await _systemAlarmService.SnoozeAsync(
            alarmId,
            request.Minutes,
            cancellationToken);

        if (!success)
        {
            return NotFound("Alarm nije pronađen.");
        }

        return NoContent();
    }

    private static SystemAlarmTriggerDto MapToDto(SystemAlarmTrigger alarm)
    {
        return new SystemAlarmTriggerDto
        {
            Id = alarm.Id,
            BusinessId = alarm.BusinessId,

            Domain = (int)alarm.Domain,
            DomainText = GetDomainText(alarm.Domain),

            AlarmType = (int)alarm.AlarmType,
            AlarmTypeText = GetAlarmTypeText(alarm.AlarmType),

            Status = (int)alarm.Status,
            StatusText = GetStatusText(alarm.Status),

            TargetType = (int)alarm.TargetType,
            TargetTypeText = GetTargetTypeText(alarm.TargetType),

            TargetUserId = alarm.TargetUserId,
            TargetOperationUnitId = alarm.TargetOperationUnitId,

            RelatedOrderId = alarm.RelatedOrderId,
            RelatedAppointmentId = alarm.RelatedAppointmentId,
            RelatedChatConversationId = alarm.RelatedChatConversationId,
            RelatedChatMessageId = alarm.RelatedChatMessageId,

            TriggerAtUtc = alarm.TriggerAtUtc,
            CreatedAtUtc = alarm.CreatedAtUtc,
            FiredAtUtc = alarm.FiredAtUtc,
            StoppedAtUtc = alarm.StoppedAtUtc,
            SnoozedUntilUtc = alarm.SnoozedUntilUtc,
            CancelledAtUtc = alarm.CancelledAtUtc,

            Title = alarm.Title,
            Message = alarm.Message,
            SoundKey = alarm.SoundKey,
            IsUrgent = alarm.IsUrgent,
            RequiresUserAction = alarm.RequiresUserAction,
            ActionKey = alarm.ActionKey,
            PayloadJson = alarm.PayloadJson
        };
    }

    private static string GetDomainText(SystemAlarmDomain domain)
    {
        return domain switch
        {
            SystemAlarmDomain.General => "Opšte",
            SystemAlarmDomain.Restaurant => "Restoran",
            SystemAlarmDomain.Appointment => "Termini",
            SystemAlarmDomain.Taxi => "Taxi",
            SystemAlarmDomain.Gym => "Teretana",
            SystemAlarmDomain.Hotel => "Hotel",
            SystemAlarmDomain.Chat => "Chat",
            SystemAlarmDomain.Billing => "Naplata",
            SystemAlarmDomain.CustomerCare => "Klijenti",
            _ => "Nepoznato"
        };
    }

    private static string GetAlarmTypeText(SystemAlarmType alarmType)
    {
        return alarmType switch
        {
            SystemAlarmType.GeneralReminder => "Opšti podsetnik",

            SystemAlarmType.RestaurantNewOrder => "Nova porudžbina",
            SystemAlarmType.RestaurantPreparationStart => "Početak pripreme",
            SystemAlarmType.RestaurantOrderLate => "Porudžbina kasni",
            SystemAlarmType.RestaurantCustomerRejectedDelay => "Klijent odbio čekanje",
            SystemAlarmType.RestaurantInternalUrgentMessage => "Hitna interna poruka",
            SystemAlarmType.RestaurantTableShouldBeFree => "Sto treba osloboditi",

            SystemAlarmType.AppointmentUpcoming => "Termin uskoro",
            SystemAlarmType.AppointmentApprovalWaiting => "Termin čeka potvrdu",
            SystemAlarmType.AppointmentCustomerRejectedProposal => "Klijent odbio predlog",

            SystemAlarmType.ChatNewMessage => "Nova poruka",
            SystemAlarmType.ChatUrgentMessage => "Hitna poruka",

            SystemAlarmType.TaxiVehicleShouldBeFree => "Vozilo bi trebalo da je slobodno",
            SystemAlarmType.TaxiUrgentRequest => "Hitan taxi zahtev",

            SystemAlarmType.GymMembershipExpiring => "Članarina ističe",
            SystemAlarmType.GymBirthday => "Rođendan",

            SystemAlarmType.BillingPaymentDue => "Dospela naplata",

            _ => "Nepoznat alarm"
        };
    }

    private static string GetStatusText(SystemAlarmStatus status)
    {
        return status switch
        {
            SystemAlarmStatus.Pending => "Čeka",
            SystemAlarmStatus.Fired => "Aktiviran",
            SystemAlarmStatus.Stopped => "Zaustavljen",
            SystemAlarmStatus.Snoozed => "Odložen",
            SystemAlarmStatus.Cancelled => "Otkazan",
            SystemAlarmStatus.Expired => "Istekao",
            _ => "Nepoznato"
        };
    }

    private static string GetTargetTypeText(SystemAlarmTargetType targetType)
    {
        return targetType switch
        {
            SystemAlarmTargetType.Business => "Biznis",
            SystemAlarmTargetType.BusinessUser => "Korisnik biznisa",
            SystemAlarmTargetType.Customer => "Klijent",
            SystemAlarmTargetType.OperationUnit => "Radna jedinica",
            SystemAlarmTargetType.Device => "Uređaj",
            _ => "Nepoznato"
        };
    }
}