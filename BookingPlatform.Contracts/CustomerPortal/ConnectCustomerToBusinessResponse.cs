namespace BookingPlatform.Contracts.CustomerPortal;

public sealed class ConnectCustomerToBusinessResponse
{
    public long BusinessId { get; set; }

    public long BusinessCustomerId { get; set; }

    public bool WasAlreadyConnected { get; set; }

    public string Message { get; set; } = string.Empty;
}