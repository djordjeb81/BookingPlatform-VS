using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BookingPlatform.Api.Services;

public sealed class AppointmentCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppointmentCleanupHostedService> _logger;

    public AppointmentCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AppointmentCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunCleanupAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                await RunCleanupAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška tokom periodičnog brisanja starih zatvorenih termina.");
            }
        }
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var cleanupService = scope.ServiceProvider.GetRequiredService<AppointmentCleanupService>();

            var deletedCount = await cleanupService.DeleteOldClosedAppointmentsAsync(cancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Obrisano je {DeletedCount} starih zatvorenih termina i povezanih podataka.",
                    deletedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška pri brisanju starih zatvorenih termina.");
        }
    }
}