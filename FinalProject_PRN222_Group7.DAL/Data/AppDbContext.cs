using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7.DAL.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Course> Courses => Set<Course>();
        public DbSet<Document> Documents => Set<Document>();
        public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
        public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
        public DbSet<Quiz> Quizzes => Set<Quiz>();
        public DbSet<Question> Questions => Set<Question>();
        public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
        public DbSet<Package> Packages => Set<Package>();
        public DbSet<PackageFeature> PackageFeatures => Set<PackageFeature>();
        public DbSet<UserPackage> UserPackages => Set<UserPackage>();
        public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
        public DbSet<CreditWallet> CreditWallets => Set<CreditWallet>();
        public DbSet<CreditTransaction> CreditTransactions => Set<CreditTransaction>();
        public DbSet<CreditPackage> CreditPackages => Set<CreditPackage>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<PaymentCallbackLog> PaymentCallbackLogs => Set<PaymentCallbackLog>();
        public DbSet<AiActionCost> AiActionCosts => Set<AiActionCost>();
        public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();
        public DbSet<BenchmarkRun> BenchmarkRuns => Set<BenchmarkRun>();
        public DbSet<UploadLog> UploadLogs => Set<UploadLog>();
        public DbSet<QuestionBankItem> QuestionBankItems => Set<QuestionBankItem>();
        public DbSet<Chapter> Chapters => Set<Chapter>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Package>().Property(p => p.Price).HasPrecision(18, 2);
            builder.Entity<CreditPackage>().Property(p => p.Price).HasPrecision(18, 2);
            builder.Entity<Payment>().Property(p => p.Amount).HasPrecision(18, 2);
            builder.Entity<ChatSession>().Property<string?>("LegacyAppUserId").HasColumnName("AppUserId");
            builder.Entity<Course>().Property<string?>("LegacyAppUserId").HasColumnName("AppUserId");
            builder.Entity<Quiz>().Property<int?>("LegacyCourseId").HasColumnName("CourseId1");
            builder.Entity<QuizAttempt>().Property<string?>("LegacyAppUserId").HasColumnName("AppUserId");
            builder.Entity<Payment>().Property<string?>("LegacyAppUserId").HasColumnName("AppUserId");
            builder.Entity<CreditWallet>().Property(w => w.RowVersion).IsRowVersion();

            builder.Entity<Package>().HasIndex(p => p.Code).IsUnique();
            builder.Entity<PackageFeature>().HasIndex(f => new { f.PackageId, f.FeatureCode }).IsUnique();
            builder.Entity<CreditPackage>().HasIndex(p => p.Code).IsUnique();
            builder.Entity<CreditTransaction>().HasIndex(t => t.RequestId);
            builder.Entity<Payment>().HasIndex(p => p.GatewayOrderCode).IsUnique().HasFilter("[GatewayOrderCode] IS NOT NULL");
            builder.Entity<PaymentCallbackLog>().HasIndex(p => new { p.GatewayProvider, p.GatewayOrderCode, p.Signature });
            builder.Entity<AiActionCost>().HasIndex(a => a.ActionCode).IsUnique();
            builder.Entity<AiUsageLog>().HasIndex(a => a.RequestId).IsUnique();
            builder.Entity<CreditWallet>().HasIndex(w => w.UserId).IsUnique();
            builder.Entity<ChatSession>().HasIndex(s => s.UserId);
            builder.Entity<Document>().HasIndex(d => d.CourseId);
            builder.Entity<Document>().HasIndex(d => d.Status);

            builder.Entity<Course>()
                .HasOne(c => c.Lecturer)
                .WithMany(u => u.Courses)
                .HasForeignKey(c => c.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Course>()
                .HasOne<AppUser>()
                .WithMany()
                .HasForeignKey("LegacyAppUserId")
                .HasConstraintName("FK_Courses_AspNetUsers_AppUserId");

            builder.Entity<Document>()
                .HasOne(d => d.Course)
                .WithMany(c => c.Documents)
                .HasForeignKey(d => d.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Document>()
                .HasOne(d => d.UploadedBy)
                .WithMany()
                .HasForeignKey(d => d.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ChatSession>()
                .HasOne(s => s.Course)
                .WithMany()
                .HasForeignKey(s => s.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ChatSession>()
                .HasOne(s => s.User)
                .WithMany(u => u.ChatSessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ChatSession>()
                .HasOne<AppUser>()
                .WithMany()
                .HasForeignKey("LegacyAppUserId")
                .HasConstraintName("FK_ChatSessions_AspNetUsers_AppUserId");

            builder.Entity<Quiz>()
                .HasOne(q => q.Course)
                .WithMany(c => c.Quizzes)
                .HasForeignKey(q => q.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Quiz>()
                .HasOne<Course>()
                .WithMany()
                .HasForeignKey("LegacyCourseId")
                .HasConstraintName("FK_Quizzes_Courses_CourseId1");

            builder.Entity<Quiz>()
                .HasOne(q => q.Document)
                .WithMany()
                .HasForeignKey(q => q.DocumentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<QuizAttempt>()
                .HasOne(a => a.User)
                .WithMany(u => u.QuizAttempts)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<QuizAttempt>()
                .HasOne<AppUser>()
                .WithMany()
                .HasForeignKey("LegacyAppUserId")
                .HasConstraintName("FK_QuizAttempts_AspNetUsers_AppUserId");

            builder.Entity<QuizAttempt>()
                .HasOne(a => a.Quiz)
                .WithMany(q => q.Attempts)
                .HasForeignKey(a => a.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<DocumentChunk>()
                .HasOne(c => c.Document)
                .WithMany(d => d.Chunks)
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Payment>()
                .HasOne(p => p.User)
                .WithMany(u => u.Payments)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Payment>()
                .HasOne<AppUser>()
                .WithMany()
                .HasForeignKey("LegacyAppUserId")
                .HasConstraintName("FK_Payments_AspNetUsers_AppUserId");

            builder.Entity<Payment>()
                .HasOne(p => p.Package)
                .WithMany(pkg => pkg.Payments)
                .HasForeignKey(p => p.PackageId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Payment>()
                .HasOne(p => p.CreditPackage)
                .WithMany(cp => cp.Payments)
                .HasForeignKey(p => p.CreditPackageId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Payment>()
                .HasOne(p => p.UserSubscription)
                .WithMany(s => s.Payments)
                .HasForeignKey(p => p.UserSubscriptionId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserPackage>()
                .HasOne(up => up.User)
                .WithMany()
                .HasForeignKey(up => up.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserPackage>()
                .HasOne(up => up.Package)
                .WithMany(pkg => pkg.UserPackages)
                .HasForeignKey(up => up.PackageId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PackageFeature>()
                .HasOne(f => f.Package)
                .WithMany(p => p.Features)
                .HasForeignKey(f => f.PackageId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserSubscription>()
                .HasOne(s => s.User)
                .WithMany(u => u.Subscriptions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserSubscription>()
                .HasOne(s => s.Package)
                .WithMany(p => p.Subscriptions)
                .HasForeignKey(s => s.PackageId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserSubscription>()
                .HasOne(s => s.NextPackage)
                .WithMany()
                .HasForeignKey(s => s.NextPackageId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<CreditWallet>()
                .HasOne(w => w.User)
                .WithOne(u => u.CreditWallet)
                .HasForeignKey<CreditWallet>(w => w.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<CreditTransaction>()
                .HasOne(t => t.CreditWallet)
                .WithMany(w => w.Transactions)
                .HasForeignKey(t => t.CreditWalletId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PaymentCallbackLog>()
                .HasOne(l => l.Payment)
                .WithMany(p => p.CallbackLogs)
                .HasForeignKey(l => l.PaymentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AiUsageLog>()
                .HasOne(l => l.User)
                .WithMany(u => u.AiUsageLogs)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AiUsageLog>()
                .HasOne(l => l.CreditTransaction)
                .WithMany()
                .HasForeignKey(l => l.CreditTransactionId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<QuestionBankItem>()
                .HasOne(q => q.Course)
                .WithMany()
                .HasForeignKey(q => q.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<QuestionBankItem>()
                .HasOne(q => q.Document)
                .WithMany()
                .HasForeignKey(q => q.DocumentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<QuestionBankItem>()
                .HasOne(q => q.Chapter)
                .WithMany(c => c.QuestionBankItems)
                .HasForeignKey(q => q.ChapterId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Chapter>()
                .HasOne(ch => ch.Course)
                .WithMany(c => c.Chapters)
                .HasForeignKey(ch => ch.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Document>()
                .HasOne(d => d.Chapter)
                .WithMany(ch => ch.Documents)
                .HasForeignKey(d => d.ChapterId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            var seedTime = new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc);

            builder.Entity<Package>().HasData(
                new Package
                {
                    Id = 1,
                    Code = "legacy-basic",
                    Name = "Basic (Legacy)",
                    Tier = PackageTier.Basic,
                    Price = 0,
                    BillingPeriod = BillingPeriod.Monthly,
                    DurationInDays = 30,
                    MonthlyCredit = 50,
                    MonthlyAiQueries = 50,
                    MaxDocuments = 5,
                    HasQuizGeneration = false,
                    HasBenchmark = false,
                    IsFree = true,
                    IsFeatured = false,
                    DisplayOrder = 90,
                    Description = "Gói cũ để giữ tương thích dữ liệu hiện tại.",
                    IsActive = false,
                    CreatedAt = seedTime,
                    UpdatedAt = seedTime
                },
                new Package
                {
                    Id = 2,
                    Code = "legacy-pro",
                    Name = "Pro (Legacy)",
                    Tier = PackageTier.Pro,
                    Price = 99000,
                    BillingPeriod = BillingPeriod.Monthly,
                    DurationInDays = 30,
                    MonthlyCredit = 500,
                    MonthlyAiQueries = 500,
                    MaxDocuments = 50,
                    HasQuizGeneration = true,
                    HasBenchmark = false,
                    IsFree = false,
                    IsFeatured = false,
                    DisplayOrder = 91,
                    Description = "Gói cũ để giữ tương thích dữ liệu hiện tại.",
                    IsActive = false,
                    CreatedAt = seedTime,
                    UpdatedAt = seedTime
                },
                new Package
                {
                    Id = 3,
                    Code = "legacy-ultra",
                    Name = "Ultra (Legacy)",
                    Tier = PackageTier.Ultra,
                    Price = 299000,
                    BillingPeriod = BillingPeriod.Monthly,
                    DurationInDays = 30,
                    MonthlyCredit = 2000,
                    MonthlyAiQueries = -1,
                    MaxDocuments = -1,
                    HasQuizGeneration = true,
                    HasBenchmark = true,
                    IsFree = false,
                    IsFeatured = false,
                    DisplayOrder = 92,
                    Description = "Gói cũ để giữ tương thích dữ liệu hiện tại.",
                    IsActive = false,
                    CreatedAt = seedTime,
                    UpdatedAt = seedTime
                },
                new Package
                {
                    Id = 10,
                    Code = "free",
                    Name = "Free",
                    Tier = PackageTier.Free,
                    Price = 0,
                    BillingPeriod = BillingPeriod.Monthly,
                    DurationInDays = 30,
                    MonthlyCredit = 50,
                    MonthlyAiQueries = 50,
                    MaxDocuments = 0,
                    HasQuizGeneration = false,
                    HasBenchmark = false,
                    IsFree = true,
                    IsFeatured = false,
                    DisplayOrder = 1,
                    Description = "Gói miễn phí cho sinh viên với credit cơ bản hằng tháng.",
                    IsActive = true,
                    CreatedAt = seedTime,
                    UpdatedAt = seedTime
                },
                new Package
                {
                    Id = 11,
                    Code = "basic",
                    Name = "Basic",
                    Tier = PackageTier.Basic,
                    Price = 49000,
                    BillingPeriod = BillingPeriod.Monthly,
                    DurationInDays = 30,
                    MonthlyCredit = 250,
                    MonthlyAiQueries = 250,
                    MaxDocuments = 0,
                    HasQuizGeneration = true,
                    HasBenchmark = false,
                    IsFree = false,
                    IsFeatured = false,
                    DisplayOrder = 2,
                    Description = "Phù hợp cho sinh viên cần dùng AI thường xuyên trong một môn học.",
                    IsActive = true,
                    CreatedAt = seedTime,
                    UpdatedAt = seedTime
                },
                new Package
                {
                    Id = 12,
                    Code = "plus",
                    Name = "Plus",
                    Tier = PackageTier.Plus,
                    Price = 99000,
                    BillingPeriod = BillingPeriod.Monthly,
                    DurationInDays = 30,
                    MonthlyCredit = 800,
                    MonthlyAiQueries = 800,
                    MaxDocuments = 0,
                    HasQuizGeneration = true,
                    HasBenchmark = false,
                    IsFree = false,
                    IsFeatured = true,
                    DisplayOrder = 3,
                    Description = "Gói phổ biến nhất cho sinh viên dùng chat, quiz và phân tích học tập nâng cao.",
                    IsActive = true,
                    CreatedAt = seedTime,
                    UpdatedAt = seedTime
                },
                new Package
                {
                    Id = 13,
                    Code = "research",
                    Name = "Research",
                    Tier = PackageTier.Research,
                    Price = 199000,
                    BillingPeriod = BillingPeriod.Monthly,
                    DurationInDays = 30,
                    MonthlyCredit = 2000,
                    MonthlyAiQueries = 2000,
                    MaxDocuments = 0,
                    HasQuizGeneration = true,
                    HasBenchmark = true,
                    IsFree = false,
                    IsFeatured = false,
                    DisplayOrder = 4,
                    Description = "Gói cao nhất cho nhu cầu nghiên cứu, tổng hợp và phân tích AI chuyên sâu.",
                    IsActive = true,
                    CreatedAt = seedTime,
                    UpdatedAt = seedTime
                }
            );

            builder.Entity<PackageFeature>().HasData(
                new PackageFeature { Id = 1, PackageId = 10, FeatureCode = "ai.chat.single_kb", FeatureName = "Chat AI theo môn học", FeatureValue = "basic", IsEnabled = true, DisplayOrder = 1 },
                new PackageFeature { Id = 2, PackageId = 10, FeatureCode = "ai.quiz.basic", FeatureName = "Làm quiz AI", FeatureValue = "false", IsEnabled = false, DisplayOrder = 2 },
                new PackageFeature { Id = 3, PackageId = 11, FeatureCode = "ai.chat.single_kb", FeatureName = "Chat AI theo môn học", FeatureValue = "standard", IsEnabled = true, DisplayOrder = 1 },
                new PackageFeature { Id = 4, PackageId = 11, FeatureCode = "ai.quiz.basic", FeatureName = "Sinh quiz cơ bản", FeatureValue = "true", IsEnabled = true, DisplayOrder = 2 },
                new PackageFeature { Id = 5, PackageId = 12, FeatureCode = "ai.chat.multi_doc", FeatureName = "Chat AI đa tài liệu", FeatureValue = "true", IsEnabled = true, DisplayOrder = 1 },
                new PackageFeature { Id = 6, PackageId = 12, FeatureCode = "ai.quiz.basic", FeatureName = "Sinh quiz AI", FeatureValue = "true", IsEnabled = true, DisplayOrder = 2 },
                new PackageFeature { Id = 7, PackageId = 12, FeatureCode = "ai.flashcard.basic", FeatureName = "Flashcard AI", FeatureValue = "true", IsEnabled = true, DisplayOrder = 3 },
                new PackageFeature { Id = 8, PackageId = 13, FeatureCode = "ai.deep_analysis", FeatureName = "Phân tích AI chuyên sâu", FeatureValue = "true", IsEnabled = true, DisplayOrder = 1 },
                new PackageFeature { Id = 9, PackageId = 13, FeatureCode = "benchmark.advanced", FeatureName = "Benchmark nâng cao", FeatureValue = "true", IsEnabled = true, DisplayOrder = 2 },
                new PackageFeature { Id = 10, PackageId = 13, FeatureCode = "priority.processing", FeatureName = "Ưu tiên xử lý", FeatureValue = "high", IsEnabled = true, DisplayOrder = 3 }
            );

            builder.Entity<CreditPackage>().HasData(
                new CreditPackage { Id = 1, Code = "credit-500", Name = "500 AI Credit", Credits = 500, Price = 29000, Description = "Bổ sung nhanh cho các tác vụ học tập hằng ngày.", IsFeatured = false, DisplayOrder = 1, IsActive = true, CreatedAt = seedTime, UpdatedAt = seedTime },
                new CreditPackage { Id = 2, Code = "credit-1500", Name = "1.500 AI Credit", Credits = 1500, Price = 79000, Description = "Gói tiết kiệm cho kỳ học có cường độ sử dụng AI cao.", IsFeatured = true, DisplayOrder = 2, IsActive = true, CreatedAt = seedTime, UpdatedAt = seedTime },
                new CreditPackage { Id = 3, Code = "credit-4000", Name = "4.000 AI Credit", Credits = 4000, Price = 179000, Description = "Phù hợp khi cần học tăng tốc hoặc ôn thi dài hạn.", IsFeatured = false, DisplayOrder = 3, IsActive = true, CreatedAt = seedTime, UpdatedAt = seedTime }
            );

            builder.Entity<AiActionCost>().HasData(
                new AiActionCost { Id = 1, ActionCode = "chat.ask", ActionName = "Hỏi đáp AI", CreditCost = 1, IsActive = true, Description = "Mỗi lượt hỏi chat AI tiêu tốn 1 credit.", DisplayOrder = 1 },
                new AiActionCost { Id = 2, ActionCode = "quiz.generate", ActionName = "Sinh quiz bằng AI", CreditCost = 5, IsActive = true, Description = "Sinh một bài quiz từ tài liệu tiêu tốn 5 credit.", DisplayOrder = 2 },
                new AiActionCost { Id = 3, ActionCode = "flashcard.generate", ActionName = "Sinh flashcard", CreditCost = 3, IsActive = true, Description = "Sinh flashcard từ tài liệu tiêu tốn 3 credit.", DisplayOrder = 3 },
                new AiActionCost { Id = 4, ActionCode = "summary.generate", ActionName = "Tóm tắt tài liệu", CreditCost = 2, IsActive = true, Description = "Tóm tắt tài liệu tiêu tốn 2 credit.", DisplayOrder = 4 }
            );
        }
    }
}
