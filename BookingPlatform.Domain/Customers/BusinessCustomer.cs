using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Customers;

public sealed class BusinessCustomer : Entity
{
    public long BusinessId { get; set; }
    public long? CustomerProfileId { get; set; }

    public CustomerProfile? CustomerProfile { get; set; }

    public long? AppUserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? RemovedFromCustomerListAtUtc { get; set; }
}