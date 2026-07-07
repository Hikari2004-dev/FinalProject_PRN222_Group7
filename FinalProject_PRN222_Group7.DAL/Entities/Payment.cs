using FinalProject_PRN222_Group7.DAL.Enums;

namespace FinalProject_PRN222_Group7.DAL.Entities;

public class Payment
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? TransactionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int PackageId { get; set; }
    public Package Package { get; set; } = null!;
}
