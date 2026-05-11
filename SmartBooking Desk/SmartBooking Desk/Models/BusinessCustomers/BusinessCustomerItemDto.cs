namespace SmartBooking_Desk.Models.BusinessCustomers;

public sealed class BusinessCustomerItemDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public long? AppUserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public string DisplayText
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(FullName))
                parts.Add(FullName);

            if (!string.IsNullOrWhiteSpace(Phone))
                parts.Add(Phone);

            if (!string.IsNullOrWhiteSpace(Email))
                parts.Add(Email);

            return string.Join("  ·  ", parts);
        }
    }
}