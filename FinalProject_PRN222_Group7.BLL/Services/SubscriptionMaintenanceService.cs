using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7.BLL.Services.Subscriptions;

public sealed record SubscriptionMaintenanceResult(
    int ExpiredSubscriptionCount,
    int ResetUsageCount,
    int ClosedPendingPaymentCount);

public interface ISubscriptionMaintenanceService
{
    Task<SubscriptionMaintenanceResult> ProcessAsync(CancellationToken cancellationToken = default);
}

public sealed class SubscriptionMaintenanceService(AppDbContext context) : ISubscriptionMaintenanceService
{
    public async Task<SubscriptionMaintenanceResult> ProcessAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var expiredSubscriptionCount = await ExpireSubscriptionsAsync(now, cancellationToken);
        var resetUsageCount = await ResetSubscriptionCreditsAsync(now, cancellationToken);
        var closedPendingPaymentCount = await CloseExpiredPaymentsAsync(now, cancellationToken);

        if (expiredSubscriptionCount > 0 || resetUsageCount > 0 || closedPendingPaymentCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return new SubscriptionMaintenanceResult(
            expiredSubscriptionCount,
            resetUsageCount,
            closedPendingPaymentCount);
    }

    private async Task<int> ExpireSubscriptionsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var subscriptions = await context.UserSubscriptions
            .Where(s => s.Status == SubscriptionStatus.Active && s.EndDate <= now)
            .ToListAsync(cancellationToken);

        foreach (var subscription in subscriptions)
        {
            subscription.Status = SubscriptionStatus.Expired;
            subscription.CancelAtPeriodEnd = false;
            subscription.UpdatedAt = now;
        }

        return subscriptions.Count;
    }

    private async Task<int> ResetSubscriptionCreditsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var subscriptions = await context.UserSubscriptions
            .Include(s => s.Package)
            .Where(s => s.Status == SubscriptionStatus.Active && s.StartDate <= now && s.EndDate > now)
            .ToListAsync(cancellationToken);

        var resetCount = 0;

        foreach (var subscription in subscriptions.Where(s => IsMonthlyResetDue(s, now)))
        {
            var wallet = await context.CreditWallets
                .FirstOrDefaultAsync(w => w.UserId == subscription.UserId, cancellationToken);

            if (wallet == null)
            {
                continue;
            }

            var creditAmount = Math.Max(subscription.Package.MonthlyCredit, subscription.Package.MonthlyAiQueries);
            if (creditAmount <= 0)
            {
                continue;
            }

            wallet.SubscriptionCreditBalance = creditAmount;
            wallet.UpdatedAt = now;
            subscription.UpdatedAt = now;

            resetCount++;
        }

        return resetCount;
    }

    private async Task<int> CloseExpiredPaymentsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var payments = await context.Payments
            .Where(p => p.Status == PaymentStatus.Pending && p.ExpiredAt != null && p.ExpiredAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var payment in payments)
        {
            payment.Status = PaymentStatus.Expired;
            payment.UpdatedAt = now;
        }

        return payments.Count;
    }

    private static bool IsMonthlyResetDue(UserSubscription subscription, DateTime now)
    {
        var resetDay = Math.Min(
            subscription.StartDate.Day,
            DateTime.DaysInMonth(now.Year, now.Month));

        return now.Day == resetDay && subscription.UpdatedAt.Date < now.Date;
    }
}
