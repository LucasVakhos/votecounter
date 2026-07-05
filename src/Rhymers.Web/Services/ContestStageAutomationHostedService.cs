using Rhymers.Web.Services;

namespace Rhymers.Web.Services;

public sealed class ContestStageAutomationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContestStageAutomationHostedService> _logger;

    public ContestStageAutomationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ContestStageAutomationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var contestService = scope.ServiceProvider.GetRequiredService<ContestService>();
                var changed = await contestService.ApplyAutomaticStageSwitchesAsync();

                if (changed > 0)
                    _logger.LogInformation("Automatic stage switching updated {Count} contest(s).", changed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute automatic contest stage switching cycle.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
