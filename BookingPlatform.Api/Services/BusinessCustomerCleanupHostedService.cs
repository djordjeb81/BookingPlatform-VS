namespace BookingPlatform.Api.Services;

public sealed class BusinessCustomerCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BusinessCustomerCleanupHostedService> _logger;

    public BusinessCustomerCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<BusinessCustomerCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunCleanupAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCleanupAsync(stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();

            var cleanupService = scope.ServiceProvider
                .GetRequiredService<BusinessCustomerCleanupService>();

            await cleanupService.CleanupRemovedBusinessCustomersAsync(
                TimeSpan.FromDays(180),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Business customer cleanup failed.");
        }
    }
}