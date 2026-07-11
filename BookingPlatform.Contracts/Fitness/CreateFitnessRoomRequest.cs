namespace BookingPlatform.Contracts.Fitness;

public sealed class CreateFitnessRoomRequest
{
    public string Name { get; set; } = string.Empty;

    public int Capacity { get; set; }

    public bool IsActive { get; set; } = true;

    public bool AllowsGroupClasses { get; set; } = true;

    public bool AllowsIndividualTraining { get; set; }

    public int DisplayOrder { get; set; }
}