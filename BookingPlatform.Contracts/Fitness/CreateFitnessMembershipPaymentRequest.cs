namespace BookingPlatform.Contracts.Fitness;

public sealed class CreateFitnessMembershipPaymentRequest
{
    public long FitnessMemberId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "RSD";

    public DateOnly PeriodStartDate { get; set; }

    public DateOnly PeriodEndDate { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    public string? PaymentMethod { get; set; }

    public string? Note { get; set; }
}