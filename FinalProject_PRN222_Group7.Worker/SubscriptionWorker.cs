using FinalProject_PRN222_Group7.BLL.Services.Subscriptions;
using Microsoft.Extensions.Options;

namespace FinalProject_PRN222_Group7.Worker;

public sealed class SubscriptionWorker(
    ILogger<SubscriptionWorker> logger,
    IServiceScopeFactory scopeFactory,
    IOptions<SubscriptionWorkerOptions> options) : BackgroundService
{
    private readonly SubscriptionWorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Subscription worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessSubscriptionsAsync(stoppingToken);
            await DelayUntilNextRunAsync(stoppingToken);
        }
    }

    private async Task ProcessSubscriptionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionMaintenanceService>();

            var result = await subscriptionService.ProcessAsync(cancellationToken);

            logger.LogInformation(
                "Subscription maintenance completed. Expired: {ExpiredCount}, Reset usages: {ResetUsageCount}, Pending payments closed: {ClosedPaymentCount}.",
                result.ExpiredSubscriptionCount,
                result.ResetUsageCount,
                result.ClosedPendingPaymentCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Subscription worker is stopping.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Subscription maintenance failed.");
        }
    }

    private async Task DelayUntilNextRunAsync(CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.CheckIntervalMinutes));
        await Task.Delay(interval, cancellationToken);
    }
}
