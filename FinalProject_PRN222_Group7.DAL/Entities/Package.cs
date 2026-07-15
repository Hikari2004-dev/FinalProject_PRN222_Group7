using System.ComponentModel.DataAnnotations;

namespace FinalProject_PRN222_Group7.DAL.Entities
{
    public enum PackageTier
    {
        Basic = 0,
        Pro = 1,
        Ultra = 2,
        Free = 3,
        Plus = 4,
        Research = 5
    }

    public enum BillingPeriod
    {
        Monthly = 1,
        Quarterly = 2,
        Yearly = 3,
        OneTime = 4
    }

    public enum SubscriptionStatus
    {
        Pending = 0,
        Active = 1,
        Cancelled = 2,
        Expired = 3
    }

    public enum CreditSourceType
    {
        Subscription = 1,
        Purchased = 2,
        Internal = 3
    }

    public enum CreditTransactionType
    {
        Grant = 1,
        Consume = 2,
        Refund = 3,
        Expire = 4,
        Adjust = 5
    }

    public enum CreditTransactionStatus
    {
        Completed = 1,
        Pending = 2,
        Refunded = 3
    }

    public enum PaymentStatus
    {
        Pending = 0,
        Processing = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4,
        Expired = 5,
        Refunded = 6
    }

    public enum PaymentPurchaseType
    {
        Subscription = 1,
        CreditTopUp = 2
    }

    public enum PaymentMethod
    {
        PayOS = 1,
        Internal = 2
    }

    public enum AiUsageStatus
    {
        Reserved = 1,
        Completed = 2,
        Refunded = 3,
        Failed = 4,
        Rejected = 5
    }

    public class Package
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public PackageTier Tier { get; set; }
        public decimal Price { get; set; }
        public BillingPeriod BillingPeriod { get; set; } = BillingPeriod.Monthly;
        public int DurationInDays { get; set; } = 30;
        public int MonthlyCredit { get; set; }
        public int MonthlyAiQueries { get; set; }
        public int MaxDocuments { get; set; }
        public bool HasQuizGeneration { get; set; }
        public bool HasBenchmark { get; set; }
        public bool IsFree { get; set; }
        public bool IsFeatured { get; set; }
        public int DisplayOrder { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PackageFeature> Features { get; set; } = new List<PackageFeature>();
        public ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
        public ICollection<UserPackage> UserPackages { get; set; } = new List<UserPackage>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }

    public class PackageFeature
    {
        public int Id { get; set; }
        public int PackageId { get; set; }
        public Package Package { get; set; } = null!;
        public string FeatureCode { get; set; } = string.Empty;
        public string FeatureName { get; set; } = string.Empty;
        public string? FeatureValue { get; set; }
        public bool IsEnabled { get; set; } = true;
        public int DisplayOrder { get; set; }
    }

    public class UserPackage
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime EndDate { get; set; }
        public int RemainingQueries { get; set; }
        public bool IsActive { get; set; } = true;
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
        public int PackageId { get; set; }
        public Package Package { get; set; } = null!;
    }

    public class UserSubscription
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
        public int PackageId { get; set; }
        public Package Package { get; set; } = null!;
        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Pending;
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime EndDate { get; set; }
        public bool AutoRenew { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
        public int? NextPackageId { get; set; }
        public Package? NextPackage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }

    public class CreditWallet
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
        public int SubscriptionCreditBalance { get; set; }
        public int PurchasedCreditBalance { get; set; }
        public int InternalCreditBalance { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public ICollection<CreditTransaction> Transactions { get; set; } = new List<CreditTransaction>();
    }

    public class CreditTransaction
    {
        public int Id { get; set; }
        public int CreditWalletId { get; set; }
        public CreditWallet CreditWallet { get; set; } = null!;
        public CreditSourceType SourceType { get; set; }
        public CreditTransactionType TransactionType { get; set; }
        public CreditTransactionStatus Status { get; set; } = CreditTransactionStatus.Completed;
        public int Credits { get; set; }
        public int BalanceAfter { get; set; }
        public string RequestId { get; set; } = string.Empty;
        public string? ReferenceCode { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CreditPackage
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Credits { get; set; }
        public decimal Price { get; set; }
        public string? Description { get; set; }
        public bool IsFeatured { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }

    public class Payment
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public PaymentPurchaseType PurchaseType { get; set; } = PaymentPurchaseType.Subscription;
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.PayOS;
        public string? TransactionId { get; set; }
        public string? GatewayOrderCode { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? Notes { get; set; }
        public string? MetadataJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }
        public DateTime? ExpiredAt { get; set; }
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
        public int? PackageId { get; set; }
        public Package? Package { get; set; }
        public int? CreditPackageId { get; set; }
        public CreditPackage? CreditPackage { get; set; }
        public int? UserSubscriptionId { get; set; }
        public UserSubscription? UserSubscription { get; set; }

        public ICollection<PaymentCallbackLog> CallbackLogs { get; set; } = new List<PaymentCallbackLog>();
    }

    public class PaymentCallbackLog
    {
        public int Id { get; set; }
        public int? PaymentId { get; set; }
        public Payment? Payment { get; set; }
        public string GatewayProvider { get; set; } = string.Empty;
        public string? GatewayOrderCode { get; set; }
        public string Signature { get; set; } = string.Empty;
        public bool IsSignatureValid { get; set; }
        public bool IsProcessed { get; set; }
        public string RawPayload { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
    }

    public class AiActionCost
    {
        public int Id { get; set; }
        public string ActionCode { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public int CreditCost { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class AiUsageLog
    {
        public int Id { get; set; }
        public string RequestId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
        public string ActionCode { get; set; } = string.Empty;
        public string? ModelName { get; set; }
        public int CreditsCharged { get; set; }
        public int TokensUsed { get; set; }
        public AiUsageStatus Status { get; set; } = AiUsageStatus.Reserved;
        public string? ErrorMessage { get; set; }
        public int? CreditTransactionId { get; set; }
        public CreditTransaction? CreditTransaction { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
