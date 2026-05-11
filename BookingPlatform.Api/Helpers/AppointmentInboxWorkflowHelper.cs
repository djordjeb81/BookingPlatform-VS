using BookingPlatform.Domain.Appointments;

namespace BookingPlatform.Api.Helpers;

public static class AppointmentInboxWorkflowHelper
{
    public static string? GetOwnerActionLabel(string? actionType)
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

    public static bool RequiresOwnerFollowUp(
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

    public static string? GetFollowUpHint(
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

    public static string? GetOwnerWorkflowState(
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

    public static string? GetOwnerWorkflowLabel(string? ownerWorkflowState)
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

    public static DateTime? TryExtractScheduledAtUtc(string? newValuesJson)
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
}