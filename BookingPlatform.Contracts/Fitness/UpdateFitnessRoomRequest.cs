namespace BookingPlatform.Contracts.Fitness;

public sealed class UpdateFitnessRoomRequest
{
    public string Name { get; set; } = string.Empty;

    public int Capacity { get; set; }

    public bool IsActive { get; set; }

    public bool AllowsGroupClasses { get; set; }

    public bool AllowsIndividualTraining { get; set; }

    public int DisplayOrder { get; set; }
}