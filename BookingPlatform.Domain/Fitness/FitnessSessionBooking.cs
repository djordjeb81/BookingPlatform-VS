using BookingPlatform.Domain.Auth;
using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;
using BookingPlatform.Domain.Customers;

namespace BookingPlatform.Domain.Fitness;

public sealed class FitnessSessionBooking : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public long FitnessSessionId { get; set; }

    public long? FitnessMemberId { get; set; }

    public FitnessMember? FitnessMember { get; set; }

    public FitnessSession FitnessSession { get; set; } = null!;

    public long? CustomerProfileId { get; set; }

    public CustomerProfile? CustomerProfile { get; set; }

    public long? BusinessCustomerId { get; set; }

    public BusinessCustomer? BusinessCustomer { get; set; }

    public long? FitnessMemberTrainingPassId { get; set; }

    public bool ConsumesTrainingPassSession { get; set; } = true;

    public FitnessMemberTrainingPass? FitnessMemberTrainingPass { get; set; }

    public long? AppUserId { get; set; }

    public AppUser? AppUser { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public FitnessSessionBookingStatus Status { get; set; } = FitnessSessionBookingStatus.Booked;

    public bool MembershipWasActiveAtBooking { get; set; } = true;

    public string? MembershipWarningText { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime? AttendedAtUtc { get; set; }

    public DateTime? NoShowAtUtc { get; set; }
}