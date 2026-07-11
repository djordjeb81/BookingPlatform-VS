using BookingPlatform.Domain.Auth;
using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Fitness;

public sealed class FitnessMembershipPayment : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public long FitnessMemberId { get; set; }

    public FitnessMember FitnessMember { get; set; } = null!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "RSD";

    public DateOnly PeriodStartDate { get; set; }

    public DateOnly PeriodEndDate { get; set; }

    public DateTime PaidAtUtc { get; set; }

    public string? PaymentMethod { get; set; }

    public string? Note { get; set; }

    public long? CreatedByUserId { get; set; }

    public AppUser? CreatedByUser { get; set; }
}