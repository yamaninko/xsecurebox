using SecureBox.Infrastructure.Services;

namespace SecureBox.API.Hosted;

public sealed class EthereumBootstrapHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<EthereumBootstrapHostedService> _logger;

    public EthereumBootstrapHostedService(IServiceScopeFactory scopes, ILogger<EthereumBootstrapHostedService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= 15 && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                if (scope.ServiceProvider.GetService<SecureBox.Core.Interfaces.IChainVerificationService>()
                    is EthereumVerificationService eth)
                {
                    var address = await eth.EnsureContractAsync(stoppingToken);
                    _logger.LogInformation("SecureBox Ethereum registry ready at {Address}", address);
                }

                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Waiting for Ethereum VM (attempt {Attempt})", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
