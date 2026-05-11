using BookingPlatform.Infrastructure.Persistence;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Services;

public sealed class FirebasePushNotificationService : IFirebasePushNotificationService
{
    private static readonly object FirebaseLock = new();
    private static bool _isFirebaseInitialized;

    private readonly BookingDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FirebasePushNotificationService> _logger;

    public FirebasePushNotificationService(
        BookingDbContext dbContext,
        IConfiguration configuration,
        ILogger<FirebasePushNotificationService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;

        EnsureFirebaseInitialized();
    }

    public async Task SendToUserAsync(
        long appUserId,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _dbContext.UserPushTokens
            .AsNoTracking()
            .Where(x =>
                x.AppUserId == appUserId &&
                x.IsActive &&
                x.Platform == "Android")
            .Select(x => x.Token)
            .Distinct()
            .ToListAsync(cancellationToken);

        await SendToTokensAsync(tokens, title, body, data, cancellationToken);
    }

    public async Task SendToBusinessUsersAsync(
        long businessId,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        var userIds = await _dbContext.BusinessUserMemberships
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.IsActive)
            .Select(x => x.AppUserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (userIds.Count == 0)
            return;

        var tokens = await _dbContext.UserPushTokens
            .AsNoTracking()
            .Where(x =>
                userIds.Contains(x.AppUserId) &&
                x.IsActive &&
                x.Platform == "Android")
            .Select(x => x.Token)
            .Distinct()
            .ToListAsync(cancellationToken);

        await SendToTokensAsync(tokens, title, body, data, cancellationToken);
    }

    private async Task SendToTokensAsync(
        List<string> tokens,
        string title,
        string body,
        Dictionary<string, string>? data,
        CancellationToken cancellationToken)
    {
        if (tokens.Count == 0)
        {
            _logger.LogInformation("Firebase push nije poslat: nema aktivnih tokena.");
            return;
        }

        foreach (var token in tokens)
        {
            try
            {
                var pushData = data is null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(data);

                pushData["title"] = title;
                pushData["body"] = body;

                var message = new Message
                {
                    Token = token,
                    Data = pushData,
                    Android = new AndroidConfig
                    {
                        Priority = Priority.High
                    }
                };

                var messageId = await FirebaseMessaging.DefaultInstance
                    .SendAsync(message, cancellationToken);

                _logger.LogInformation(
                    "Firebase push poslat. MessageId: {MessageId}, Token {TokenPrefix}.",
                    messageId,
                    token.Length > 12 ? token[..12] : token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Firebase push nije poslat za token {TokenPrefix}.",
                    token.Length > 12 ? token[..12] : token);
            }
        }
    }

    private void EnsureFirebaseInitialized()
    {
        if (_isFirebaseInitialized)
            return;

        lock (FirebaseLock)
        {
            if (_isFirebaseInitialized)
                return;

            var serviceAccountPath = _configuration["Firebase:ServiceAccountPath"];

            if (string.IsNullOrWhiteSpace(serviceAccountPath))
                throw new InvalidOperationException("Firebase:ServiceAccountPath nije podešen.");

            if (!File.Exists(serviceAccountPath))
                throw new FileNotFoundException("Firebase service account fajl nije pronađen.", serviceAccountPath);

            using var stream = File.OpenRead(serviceAccountPath);

            var credential = GoogleCredential.FromStream(stream);

            FirebaseApp.Create(new AppOptions
            {
                Credential = credential
            });

            _isFirebaseInitialized = true;
        }
    }
}