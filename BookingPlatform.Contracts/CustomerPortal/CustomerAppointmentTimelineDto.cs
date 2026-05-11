namespace BookingPlatform.Contracts.CustomerPortal;

public sealed class CustomerAppointmentTimelineDto
{
    public long AppointmentId { get; set; }

    public string Text { get; set; } = string.Empty;

    public List<CustomerAppointmentTimelineStepDto> Steps { get; set; } = new();
}

public sealed class CustomerAppointmentTimelineStepDto
{
    public int StartMinute { get; set; }

    public int DurationMin { get; set; }

    public int EndMinute => StartMinute + DurationMin;

    public string Title { get; set; } = string.Empty;
}