namespace BookingPlatform.Contracts.Fitness;

public sealed class FitnessMembershipPaymentDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public long FitnessMemberId { get; set; }

    public string FitnessMemberName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "RSD";

    public DateOnly PeriodStartDate { get; set; }

    public DateOnly PeriodEndDate { get; set; }

    public DateTime PaidAtUtc { get; set; }

    public string? PaymentMethod { get; set; }

    public string? Note { get; set; }

    public long? CreatedByUserId { get; set; }

    public string? CreatedByUserName { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}