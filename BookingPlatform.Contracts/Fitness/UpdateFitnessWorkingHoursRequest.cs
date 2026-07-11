namespace BookingPlatform.Contracts.Fitness;

public sealed class UpdateFitnessWorkingHoursRequest
{
    public List<UpdateFitnessWorkingHourItemRequest> Items { get; set; } = new();
}

public sealed class UpdateFitnessWorkingHourItemRequest
{
    public int DayOfWeek { get; set; }

    public bool IsClosed { get; set; }

    public TimeOnly? OpenTime { get; set; }

    public TimeOnly? CloseTime { get; set; }
}