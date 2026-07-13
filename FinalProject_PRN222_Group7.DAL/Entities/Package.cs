namespace FinalProject_PRN222_Group7.DAL.Entities
{
    public enum PackageTier { Basic, Pro, Ultra }

    public class Package
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public PackageTier Tier { get; set; }
        public decimal Price { get; set; }
        public int MonthlyAiQueries { get; set; } // -1 = unlimited
        public int MaxDocuments { get; set; }
        public bool HasQuizGeneration { get; set; }
        public bool HasBenchmark { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UserPackage
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime EndDate { get; set; }
        public int RemainingQueries { get; set; }
        public bool IsActive { get; set; } = true;

        // FK
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
        public int PackageId { get; set; }
        public Package Package { get; set; } = null!;
    }

    public enum PaymentStatus { Pending, Completed, Failed, Refunded }

    public class Payment
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public string? TransactionId { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }

        // FK
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
        public int PackageId { get; set; }
        public Package Package { get; set; } = null!;
    }
}
