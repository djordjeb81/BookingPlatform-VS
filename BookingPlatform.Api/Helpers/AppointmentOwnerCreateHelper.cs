namespace BookingPlatform.Api.Helpers;

public static class AppointmentOwnerCreateHelper
{
    public static string BuildOwnerCreateAuditMessage(
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

    public static string BuildOwnerCreateResponseMessage(
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

    public static string GetOwnerCreateCreationMode(
        bool bypassedSlotGrid,
        bool bypassedWorkingHours,
        bool bypassedTimeOffBlocks,
        bool bypassedAppointmentConflicts)
    {
        if (!bypassedSlotGrid && !bypassedWorkingHours && !bypassedTimeOffBlocks && !bypassedAppointmentConflicts)
            return "normal";

        return "manual_override";
    }

    public static string GetOwnerCreateCreationModeLabel(string creationMode)
    {
        return creationMode switch
        {
            "normal" => "Termin je unet regularno",
            "manual_override" => "Termin je unet uz ručno odobrenje",
            _ => "Termin je unet"
        };
    }

    public static List<string> GetOwnerCreateAppliedOverrides(
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

    public static List<string> GetOwnerCreateAppliedOverrideLabels(
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
}