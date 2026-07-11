namespace BookingPlatform.Contracts.Fitness;

public sealed class UpdateFitnessMemberTrainingPassRequest
{
    public DateOnly ValidFromDate { get; set; }

    public DateOnly ValidToDate { get; set; }

    public decimal PricePaid { get; set; }

    public string Currency { get; set; } = "RSD";

    public DateTime PaidAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Note { get; set; }
}