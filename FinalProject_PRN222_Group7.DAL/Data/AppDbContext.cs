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
        public DbSet<UserPackage> UserPackages => Set<UserPackage>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<BenchmarkRun> BenchmarkRuns => Set<BenchmarkRun>();
        public DbSet<UploadLog> UploadLogs => Set<UploadLog>();
        public DbSet<QuestionBankItem> QuestionBankItems => Set<QuestionBankItem>();
        public DbSet<Chapter> Chapters => Set<Chapter>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Precision for decimal
            builder.Entity<Package>().Property(p => p.Price).HasPrecision(18, 2);
            builder.Entity<Payment>().Property(p => p.Amount).HasPrecision(18, 2);

            // Indexes
            builder.Entity<ChatSession>().HasIndex(s => s.UserId);
            builder.Entity<Document>().HasIndex(d => d.CourseId);
            builder.Entity<Document>().HasIndex(d => d.Status);

            // Fix cascade cycles - SQL Server limitation
            builder.Entity<Course>()
                .HasOne(c => c.Lecturer)
                .WithMany()
                .HasForeignKey(c => c.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);

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
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Quiz>()
                .HasOne(q => q.Course)
                .WithMany()
                .HasForeignKey(q => q.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Quiz>()
                .HasOne(q => q.Document)
                .WithMany()
                .HasForeignKey(q => q.DocumentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<QuizAttempt>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

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
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Payment>()
                .HasOne(p => p.Package)
                .WithMany()
                .HasForeignKey(p => p.PackageId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserPackage>()
                .HasOne(up => up.User)
                .WithMany()
                .HasForeignKey(up => up.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserPackage>()
                .HasOne(up => up.Package)
                .WithMany()
                .HasForeignKey(up => up.PackageId)
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

            // Seed packages
            builder.Entity<Package>().HasData(
                new Package { Id = 1, Name = "Basic", Tier = PackageTier.Basic, Price = 0, MonthlyAiQueries = 50, MaxDocuments = 5, HasQuizGeneration = false, HasBenchmark = false, Description = "Gói miễn phí cho sinh viên" },
                new Package { Id = 2, Name = "Pro", Tier = PackageTier.Pro, Price = 99000, MonthlyAiQueries = 500, MaxDocuments = 50, HasQuizGeneration = true, HasBenchmark = false, Description = "Gói nâng cao cho giảng viên" },
                new Package { Id = 3, Name = "Ultra", Tier = PackageTier.Ultra, Price = 299000, MonthlyAiQueries = -1, MaxDocuments = -1, HasQuizGeneration = true, HasBenchmark = true, Description = "Không giới hạn - full features" }
            );
        }
    }
}
