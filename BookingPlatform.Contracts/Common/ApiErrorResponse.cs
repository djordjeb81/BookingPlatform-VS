namespace BookingPlatform.Contracts.Common;

public sealed class ApiErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public List<string> ReasonCodes { get; set; } = new();
}