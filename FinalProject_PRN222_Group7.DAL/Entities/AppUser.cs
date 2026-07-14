using Microsoft.AspNetCore.Identity;

namespace FinalProject_PRN222_Group7.DAL.Entities
{
    public class AppUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        public ICollection<Course> Courses { get; set; } = new List<Course>();
        public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
        public ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
        public ICollection<AiUsageLog> AiUsageLogs { get; set; } = new List<AiUsageLog>();
        public UserPackage? UserPackage { get; set; }
        public CreditWallet? CreditWallet { get; set; }
    }
}
