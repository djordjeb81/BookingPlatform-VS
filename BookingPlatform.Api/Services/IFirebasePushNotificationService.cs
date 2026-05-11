namespace BookingPlatform.Api.Services;

public interface IFirebasePushNotificationService
{
    Task SendToUserAsync(
        long appUserId,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);

    Task SendToBusinessUsersAsync(
        long businessId,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}