using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7.BLL.Services
{
    public record WalletBalanceSummary(int SubscriptionCredits, int PurchasedCredits, int InternalCredits)
    {
        public int TotalCredits => SubscriptionCredits + PurchasedCredits + InternalCredits;
    }

    public record CreditReservationResult(
        bool Success,
        string? ErrorMessage,
        int Credits,
        int? TransactionId,
        CreditSourceType? SourceType,
        int BalanceAfter);

    public record AiUsageExecutionPayload<T>(T Payload, int TokensUsed, string? ModelName = null);

    public record AiUsageExecutionResult<T>(
        bool Success,
        T? Payload,
        string? ErrorMessage,
        int TokensUsed,
        int CreditsCharged,
        string? ModelName);

    public interface ISubscriptionService
    {
        Task EnsureCompatibilityAsync(string userId);
        Task<UserSubscription?> GetActiveSubscriptionAsync(string userId);
        Task<UserPackage?> GetLegacyCompatiblePackageAsync(string userId);
        Task<UserSubscription> ActivatePackageAsync(string userId, int packageId);
        Task<bool> HasFeatureAsync(string userId, string featureCode, IEnumerable<string>? roles = null);
        Task<int> GetDisplayedRemainingCreditsAsync(string userId, IEnumerable<string>? roles = null);
    }

    public interface ICreditWalletService
    {
        Task<CreditWallet> EnsureWalletAsync(string userId, IEnumerable<string>? roles = null);
        Task<WalletBalanceSummary> GetBalanceAsync(string userId, IEnumerable<string>? roles = null);
        Task<int> GetAvailableCreditsAsync(string userId, IEnumerable<string>? roles = null);
        Task<bool> HasCreditsAsync(string userId, int requiredCredits, IEnumerable<string>? roles = null);
        Task<CreditTransaction> GrantAsync(string userId, CreditSourceType sourceType, int credits, string requestId, string description, IEnumerable<string>? roles = null);
        Task<CreditReservationResult> ReserveAsync(string userId, int credits, string requestId, string description, IEnumerable<string>? roles = null);
        Task CompleteAsync(int transactionId, string? referenceCode = null);
        Task RefundAsync(int transactionId, string? referenceCode = null, string? description = null);
    }

    public interface IAiUsageGate
    {
        Task<AiUsageExecutionResult<T>> ExecuteAsync<T>(
            string userId,
            IEnumerable<string> roles,
            string actionCode,
            Func<Task<AiUsageExecutionPayload<T>>> operation,
            string? requestId = null);

        Task<int> GetAvailableCreditsAsync(string userId, IEnumerable<string> roles);
    }

    public class SubscriptionService : ISubscriptionService
    {
        private readonly AppDbContext _context;
        private readonly ICreditWalletService _walletService;

        public SubscriptionService(AppDbContext context, ICreditWalletService walletService)
        {
            _context = context;
            _walletService = walletService;
        }

        public async Task EnsureCompatibilityAsync(string userId)
        {
            var activeSubscription = await _context.UserSubscriptions
                .Include(s => s.Package)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active && s.EndDate > DateTime.UtcNow);
            if (activeSubscription != null)
            {
                return;
            }

            var legacyPackage = await _context.UserPackages
                .Include(up => up.Package)
                .FirstOrDefaultAsync(up => up.UserId == userId && up.IsActive && up.EndDate > DateTime.UtcNow);
            if (legacyPackage == null)
            {
                return;
            }

            var targetPackage = await ResolveCompatiblePackageAsync(legacyPackage.Package);
            if (targetPackage == null)
            {
                return;
            }

            var subscription = new UserSubscription
            {
                UserId = userId,
                PackageId = targetPackage.Id,
                Status = SubscriptionStatus.Active,
                StartDate = legacyPackage.StartDate,
                EndDate = legacyPackage.EndDate,
                AutoRenew = false,
                CancelAtPeriodEnd = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserSubscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            var wallet = await _walletService.EnsureWalletAsync(userId);
            if (wallet.SubscriptionCreditBalance < legacyPackage.RemainingQueries)
            {
                wallet.SubscriptionCreditBalance = legacyPackage.RemainingQueries;
                wallet.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<UserSubscription?> GetActiveSubscriptionAsync(string userId)
        {
            await EnsureCompatibilityAsync(userId);
            return await _context.UserSubscriptions
                .Include(s => s.Package)
                    .ThenInclude(p => p.Features)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active && s.EndDate > DateTime.UtcNow);
        }

        public async Task<UserPackage?> GetLegacyCompatiblePackageAsync(string userId)
        {
            var activeSubscription = await GetActiveSubscriptionAsync(userId);
            if (activeSubscription == null)
            {
                return await _context.UserPackages
                    .Include(up => up.Package)
                    .FirstOrDefaultAsync(up => up.UserId == userId && up.IsActive && up.EndDate > DateTime.UtcNow);
            }

            var wallet = await _walletService.EnsureWalletAsync(userId);
            return new UserPackage
            {
                UserId = userId,
                PackageId = activeSubscription.PackageId,
                Package = activeSubscription.Package,
                StartDate = activeSubscription.StartDate,
                EndDate = activeSubscription.EndDate,
                IsActive = activeSubscription.Status == SubscriptionStatus.Active,
                RemainingQueries = wallet.SubscriptionCreditBalance + wallet.PurchasedCreditBalance
            };
        }

        public async Task<UserSubscription> ActivatePackageAsync(string userId, int packageId)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.Id == packageId && p.IsActive)
                ?? throw new InvalidOperationException("Package not found");

            var now = DateTime.UtcNow;
            var existingSubscriptions = await _context.UserSubscriptions
                .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
                .ToListAsync();
            foreach (var subscription in existingSubscriptions)
            {
                subscription.Status = SubscriptionStatus.Cancelled;
                subscription.CancelAtPeriodEnd = false;
                subscription.UpdatedAt = now;
            }

            var newSubscription = new UserSubscription
            {
                UserId = userId,
                PackageId = package.Id,
                Status = SubscriptionStatus.Active,
                StartDate = now,
                EndDate = now.AddDays(package.DurationInDays > 0 ? package.DurationInDays : 30),
                AutoRenew = package.Price > 0,
                CancelAtPeriodEnd = false,
                CreatedAt = now,
                UpdatedAt = now
            };
            _context.UserSubscriptions.Add(newSubscription);

            var wallet = await _walletService.EnsureWalletAsync(userId);
            wallet.SubscriptionCreditBalance = Math.Max(package.MonthlyCredit, package.MonthlyAiQueries > 0 ? package.MonthlyAiQueries : 0);
            wallet.UpdatedAt = now;

            var legacyPackages = await _context.UserPackages
                .Where(up => up.UserId == userId && up.IsActive)
                .ToListAsync();
            foreach (var legacy in legacyPackages)
            {
                legacy.IsActive = false;
            }

            _context.UserPackages.Add(new UserPackage
            {
                UserId = userId,
                PackageId = package.Id,
                StartDate = newSubscription.StartDate,
                EndDate = newSubscription.EndDate,
                RemainingQueries = wallet.SubscriptionCreditBalance,
                IsActive = true
            });

            await _context.SaveChangesAsync();
            return newSubscription;
        }

        public async Task<bool> HasFeatureAsync(string userId, string featureCode, IEnumerable<string>? roles = null)
        {
            var roleSet = NormalizeRoles(roles);
            if (IsStaff(roleSet))
            {
                return true;
            }

            var activeSubscription = await GetActiveSubscriptionAsync(userId);
            if (activeSubscription == null)
            {
                return false;
            }

            return activeSubscription.Package.Features.Any(f => f.FeatureCode == featureCode && f.IsEnabled);
        }

        public async Task<int> GetDisplayedRemainingCreditsAsync(string userId, IEnumerable<string>? roles = null)
        {
            var roleSet = NormalizeRoles(roles);
            if (IsStaff(roleSet))
            {
                var wallet = await _walletService.EnsureWalletAsync(userId, roleSet);
                return wallet.InternalCreditBalance;
            }

            var walletSummary = await _walletService.GetBalanceAsync(userId, roleSet);
            return walletSummary.SubscriptionCredits + walletSummary.PurchasedCredits;
        }

        private async Task<Package?> ResolveCompatiblePackageAsync(Package legacyPackage)
        {
            if (!string.IsNullOrWhiteSpace(legacyPackage.Code) && !legacyPackage.Code.StartsWith("legacy-", StringComparison.OrdinalIgnoreCase))
            {
                return legacyPackage;
            }

            var targetCode = legacyPackage.Tier switch
            {
                PackageTier.Ultra => "research",
                PackageTier.Pro => "plus",
                _ => "free"
            };

            return await _context.Packages.FirstOrDefaultAsync(p => p.Code == targetCode && p.IsActive);
        }

        private static HashSet<string> NormalizeRoles(IEnumerable<string>? roles)
            => roles == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : roles.Where(r => !string.IsNullOrWhiteSpace(r)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        private static bool IsStaff(HashSet<string> roles)
            => roles.Contains("Admin") || roles.Contains("Lecturer");
    }

    public class CreditWalletService : ICreditWalletService
    {
        private readonly AppDbContext _context;

        public CreditWalletService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreditWallet> EnsureWalletAsync(string userId, IEnumerable<string>? roles = null)
        {
            var wallet = await _context.CreditWallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet != null)
            {
                return wallet;
            }

            var roleSet = NormalizeRoles(roles);
            wallet = new CreditWallet
            {
                UserId = userId,
                SubscriptionCreditBalance = 0,
                PurchasedCreditBalance = 0,
                InternalCreditBalance = IsStaff(roleSet) ? 10000 : 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.CreditWallets.Add(wallet);
            await _context.SaveChangesAsync();
            return wallet;
        }

        public async Task<WalletBalanceSummary> GetBalanceAsync(string userId, IEnumerable<string>? roles = null)
        {
            var wallet = await EnsureWalletAsync(userId, roles);
            return new WalletBalanceSummary(wallet.SubscriptionCreditBalance, wallet.PurchasedCreditBalance, wallet.InternalCreditBalance);
        }

        public async Task<int> GetAvailableCreditsAsync(string userId, IEnumerable<string>? roles = null)
        {
            var balance = await GetBalanceAsync(userId, roles);
            var roleSet = NormalizeRoles(roles);
            if (IsStaff(roleSet))
            {
                return balance.InternalCredits;
            }

            return balance.SubscriptionCredits + balance.PurchasedCredits;
        }

        public async Task<bool> HasCreditsAsync(string userId, int requiredCredits, IEnumerable<string>? roles = null)
            => await GetAvailableCreditsAsync(userId, roles) >= requiredCredits;

        public async Task<CreditTransaction> GrantAsync(string userId, CreditSourceType sourceType, int credits, string requestId, string description, IEnumerable<string>? roles = null)
        {
            if (credits <= 0)
            {
                throw new InvalidOperationException("Credits must be greater than zero");
            }

            var existing = await _context.CreditTransactions.FirstOrDefaultAsync(t => t.RequestId == requestId);
            if (existing != null)
            {
                return existing;
            }

            var wallet = await EnsureWalletAsync(userId, roles);
            ApplyBalance(wallet, sourceType, credits);
            wallet.UpdatedAt = DateTime.UtcNow;

            var transaction = new CreditTransaction
            {
                CreditWalletId = wallet.Id,
                SourceType = sourceType,
                TransactionType = CreditTransactionType.Grant,
                Status = CreditTransactionStatus.Completed,
                Credits = credits,
                BalanceAfter = GetSourceBalance(wallet, sourceType),
                RequestId = requestId,
                Description = description,
                CreatedAt = DateTime.UtcNow
            };

            _context.CreditTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            return transaction;
        }

        public async Task<CreditReservationResult> ReserveAsync(string userId, int credits, string requestId, string description, IEnumerable<string>? roles = null)
        {
            if (credits <= 0)
            {
                return new CreditReservationResult(true, null, 0, null, null, 0);
            }

            var existing = await _context.CreditTransactions.FirstOrDefaultAsync(t => t.RequestId == requestId);
            if (existing != null)
            {
                return new CreditReservationResult(true, null, existing.Credits, existing.Id, existing.SourceType, existing.BalanceAfter);
            }

            var roleSet = NormalizeRoles(roles);
            var wallet = await EnsureWalletAsync(userId, roleSet);
            var sourceType = ResolveSourceType(wallet, roleSet, credits);
            if (sourceType == null)
            {
                return new CreditReservationResult(false, "Không đủ AI credit để thực hiện thao tác này.", credits, null, null, 0);
            }

            ApplyBalance(wallet, sourceType.Value, -credits);
            wallet.UpdatedAt = DateTime.UtcNow;

            var transaction = new CreditTransaction
            {
                CreditWalletId = wallet.Id,
                SourceType = sourceType.Value,
                TransactionType = CreditTransactionType.Consume,
                Status = CreditTransactionStatus.Pending,
                Credits = credits,
                BalanceAfter = GetSourceBalance(wallet, sourceType.Value),
                RequestId = requestId,
                Description = description,
                CreatedAt = DateTime.UtcNow
            };

            _context.CreditTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            return new CreditReservationResult(true, null, credits, transaction.Id, sourceType.Value, transaction.BalanceAfter);
        }

        public async Task CompleteAsync(int transactionId, string? referenceCode = null)
        {
            var transaction = await _context.CreditTransactions.FirstOrDefaultAsync(t => t.Id == transactionId)
                ?? throw new InvalidOperationException("Credit transaction not found");

            transaction.Status = CreditTransactionStatus.Completed;
            transaction.ReferenceCode = referenceCode ?? transaction.ReferenceCode;
            await _context.SaveChangesAsync();
        }

        public async Task RefundAsync(int transactionId, string? referenceCode = null, string? description = null)
        {
            var transaction = await _context.CreditTransactions.FirstOrDefaultAsync(t => t.Id == transactionId)
                ?? throw new InvalidOperationException("Credit transaction not found");

            if (transaction.Status == CreditTransactionStatus.Refunded)
            {
                return;
            }

            var refundRequestId = $"refund:{transaction.RequestId}";
            var existingRefund = await _context.CreditTransactions.FirstOrDefaultAsync(t => t.RequestId == refundRequestId);
            if (existingRefund != null)
            {
                transaction.Status = CreditTransactionStatus.Refunded;
                await _context.SaveChangesAsync();
                return;
            }

            var wallet = await _context.CreditWallets.FirstAsync(w => w.Id == transaction.CreditWalletId);
            ApplyBalance(wallet, transaction.SourceType, transaction.Credits);
            wallet.UpdatedAt = DateTime.UtcNow;

            _context.CreditTransactions.Add(new CreditTransaction
            {
                CreditWalletId = wallet.Id,
                SourceType = transaction.SourceType,
                TransactionType = CreditTransactionType.Refund,
                Status = CreditTransactionStatus.Completed,
                Credits = transaction.Credits,
                BalanceAfter = GetSourceBalance(wallet, transaction.SourceType),
                RequestId = refundRequestId,
                ReferenceCode = referenceCode,
                Description = description ?? $"Refund for {transaction.RequestId}",
                CreatedAt = DateTime.UtcNow
            });

            transaction.Status = CreditTransactionStatus.Refunded;
            transaction.ReferenceCode = referenceCode ?? transaction.ReferenceCode;
            await _context.SaveChangesAsync();
        }

        private static CreditSourceType? ResolveSourceType(CreditWallet wallet, HashSet<string> roles, int credits)
        {
            if (IsStaff(roles))
            {
                return wallet.InternalCreditBalance >= credits ? CreditSourceType.Internal : null;
            }

            if (wallet.SubscriptionCreditBalance >= credits)
            {
                return CreditSourceType.Subscription;
            }

            if (wallet.PurchasedCreditBalance >= credits)
            {
                return CreditSourceType.Purchased;
            }

            return null;
        }

        private static int GetSourceBalance(CreditWallet wallet, CreditSourceType sourceType)
            => sourceType switch
            {
                CreditSourceType.Subscription => wallet.SubscriptionCreditBalance,
                CreditSourceType.Purchased => wallet.PurchasedCreditBalance,
                CreditSourceType.Internal => wallet.InternalCreditBalance,
                _ => 0
            };

        private static void ApplyBalance(CreditWallet wallet, CreditSourceType sourceType, int delta)
        {
            switch (sourceType)
            {
                case CreditSourceType.Subscription:
                    wallet.SubscriptionCreditBalance += delta;
                    break;
                case CreditSourceType.Purchased:
                    wallet.PurchasedCreditBalance += delta;
                    break;
                case CreditSourceType.Internal:
                    wallet.InternalCreditBalance += delta;
                    break;
            }
        }

        private static HashSet<string> NormalizeRoles(IEnumerable<string>? roles)
            => roles == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : roles.Where(r => !string.IsNullOrWhiteSpace(r)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        private static bool IsStaff(HashSet<string> roles)
            => roles.Contains("Admin") || roles.Contains("Lecturer");
    }

    public class AiUsageGate : IAiUsageGate
    {
        private readonly AppDbContext _context;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ICreditWalletService _walletService;

        public AiUsageGate(AppDbContext context, ISubscriptionService subscriptionService, ICreditWalletService walletService)
        {
            _context = context;
            _subscriptionService = subscriptionService;
            _walletService = walletService;
        }

        public Task<int> GetAvailableCreditsAsync(string userId, IEnumerable<string> roles)
            => _walletService.GetAvailableCreditsAsync(userId, roles);

        public async Task<AiUsageExecutionResult<T>> ExecuteAsync<T>(
            string userId,
            IEnumerable<string> roles,
            string actionCode,
            Func<Task<AiUsageExecutionPayload<T>>> operation,
            string? requestId = null)
        {
            var roleSet = NormalizeRoles(roles);
            var usageRequestId = requestId ?? $"{actionCode}:{Guid.NewGuid():N}";
            var requiredFeature = ResolveRequiredFeature(actionCode);

            if (requiredFeature != null)
            {
                var hasFeature = await _subscriptionService.HasFeatureAsync(userId, requiredFeature, roleSet);
                if (!hasFeature)
                {
                    await LogRejectedUsageAsync(userId, actionCode, usageRequestId, "Gói hiện tại không hỗ trợ tính năng này.");
                    return new AiUsageExecutionResult<T>(false, default, "Gói hiện tại không hỗ trợ tính năng này.", 0, 0, null);
                }
            }

            var cost = await _context.AiActionCosts
                .Where(a => a.ActionCode == actionCode && a.IsActive)
                .Select(a => a.CreditCost)
                .FirstOrDefaultAsync();
            if (cost <= 0)
            {
                cost = 1;
            }

            var reservation = await _walletService.ReserveAsync(userId, cost, usageRequestId, $"AI action: {actionCode}", roleSet);
            if (!reservation.Success)
            {
                await LogRejectedUsageAsync(userId, actionCode, usageRequestId, reservation.ErrorMessage ?? "Không đủ AI credit.");
                return new AiUsageExecutionResult<T>(false, default, reservation.ErrorMessage ?? "Không đủ AI credit.", 0, 0, null);
            }

            var usageLog = new AiUsageLog
            {
                RequestId = usageRequestId,
                UserId = userId,
                ActionCode = actionCode,
                CreditsCharged = cost,
                Status = AiUsageStatus.Reserved,
                CreditTransactionId = reservation.TransactionId,
                CreatedAt = DateTime.UtcNow
            };
            _context.AiUsageLogs.Add(usageLog);
            await _context.SaveChangesAsync();

            try
            {
                var result = await operation();
                usageLog.Status = AiUsageStatus.Completed;
                usageLog.TokensUsed = result.TokensUsed;
                usageLog.ModelName = result.ModelName;
                usageLog.CompletedAt = DateTime.UtcNow;
                await _walletService.CompleteAsync(reservation.TransactionId!.Value, usageRequestId);
                await _context.SaveChangesAsync();

                return new AiUsageExecutionResult<T>(true, result.Payload, null, result.TokensUsed, cost, result.ModelName);
            }
            catch (Exception ex)
            {
                usageLog.Status = AiUsageStatus.Refunded;
                usageLog.ErrorMessage = ex.Message;
                usageLog.CompletedAt = DateTime.UtcNow;
                await _walletService.RefundAsync(reservation.TransactionId!.Value, usageRequestId, ex.Message);
                await _context.SaveChangesAsync();
                return new AiUsageExecutionResult<T>(false, default, ex.Message, 0, cost, null);
            }
        }

        private async Task LogRejectedUsageAsync(string userId, string actionCode, string requestId, string errorMessage)
        {
            var existing = await _context.AiUsageLogs.FirstOrDefaultAsync(l => l.RequestId == requestId);
            if (existing != null)
            {
                return;
            }

            _context.AiUsageLogs.Add(new AiUsageLog
            {
                RequestId = requestId,
                UserId = userId,
                ActionCode = actionCode,
                CreditsCharged = 0,
                TokensUsed = 0,
                Status = AiUsageStatus.Rejected,
                ErrorMessage = errorMessage,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        private static string? ResolveRequiredFeature(string actionCode)
            => actionCode switch
            {
                "chat.ask" => "ai.chat.single_kb",
                "quiz.generate" => "ai.quiz.basic",
                "flashcard.generate" => "ai.flashcard.basic",
                "summary.generate" => "ai.chat.single_kb",
                _ => null
            };

        private static HashSet<string> NormalizeRoles(IEnumerable<string>? roles)
            => roles == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : roles.Where(r => !string.IsNullOrWhiteSpace(r)).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
