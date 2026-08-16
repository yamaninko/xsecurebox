using SecureBox.Infrastructure.Services;

namespace SecureBox.API.Hosted;

public sealed class LifecycleHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LifecycleHostedService> _logger;

    public LifecycleHostedService(IServiceScopeFactory scopeFactory, ILogger<LifecycleHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SweepSafe(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SweepSafe(stoppingToken);
        }
    }

    private async Task SweepSafe(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var lifecycle = scope.ServiceProvider.GetRequiredService<ILifecycleService>();
            await lifecycle.SweepAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Expiry sweep failed");
        }
    }
}
