using BookingPlatform.Domain.Auth;
using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;
using BookingPlatform.Domain.Customers;

namespace BookingPlatform.Domain.Fitness;

public sealed class FitnessMember : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public long? CustomerProfileId { get; set; }

    public CustomerProfile? CustomerProfile { get; set; }

    public long? BusinessCustomerId { get; set; }

    public BusinessCustomer? BusinessCustomer { get; set; }

    public long? AppUserId { get; set; }

    public AppUser? AppUser { get; set; }

    public List<FitnessMemberTrainingPass> TrainingPasses { get; set; } = new();

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? MemberCode { get; set; }

    public bool IsActive { get; set; } = true;

    public List<FitnessMembershipPayment> Payments { get; set; } = new();
}