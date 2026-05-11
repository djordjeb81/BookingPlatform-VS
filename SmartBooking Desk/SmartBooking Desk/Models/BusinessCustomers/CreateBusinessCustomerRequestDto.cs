namespace SmartBooking_Desk.Models.BusinessCustomers;

public sealed class CreateBusinessCustomerRequestDto
{
    public long BusinessId { get; set; }

    public long? AppUserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Notes { get; set; }
}