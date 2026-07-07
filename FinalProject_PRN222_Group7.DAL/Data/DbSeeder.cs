using System.Security.Cryptography;
using FinalProject_PRN222_Group7.DAL.Entities;
using FinalProject_PRN222_Group7.DAL.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinalProject_PRN222_Group7.DAL.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.MigrateAsync();

        if (await context.Users.AnyAsync())
            return;

        // Users
        var admin = new User
        {
            FullName = "Admin User",
            Email = "admin@example.com",
            PasswordHash = HashPassword("Admin@123"),
            Role = UserRole.Admin
        };

        var lecturer = new User
        {
            FullName = "Lecturer User",
            Email = "lecturer@example.com",
            PasswordHash = HashPassword("Lecturer@123"),
            Role = UserRole.Lecturer
        };

        var student = new User
        {
            FullName = "Student User",
            Email = "student@example.com",
            PasswordHash = HashPassword("Student@123"),
            Role = UserRole.Student
        };

        context.Users.AddRange(admin, lecturer, student);

        // Courses
        var courseAI = new Course
        {
            Name = "Introduction to Artificial Intelligence",
            Description = "A comprehensive course covering fundamental AI concepts, machine learning, and natural language processing."
        };

        var courseWeb = new Course
        {
            Name = "Web Development with ASP.NET Core",
            Description = "Learn to build modern web applications using ASP.NET Core, Razor Pages, and Entity Framework."
        };

        context.Courses.AddRange(courseAI, courseWeb);

        // Packages
        var basicPkg = new Package { Name = "Basic", Type = PackageType.Basic, Price = 0, MaxQuestionsPerDay = 5, DurationDays = 30, Description = "Free plan with limited questions" };
        var proPkg = new Package { Name = "Pro", Type = PackageType.Pro, Price = 99000, MaxQuestionsPerDay = 50, DurationDays = 30, Description = "Pro plan with more questions" };
        var ultraPkg = new Package { Name = "Ultra", Type = PackageType.Ultra, Price = 199000, MaxQuestionsPerDay = 999, DurationDays = 30, Description = "Unlimited access" };

        context.Packages.AddRange(basicPkg, proPkg, ultraPkg);
        await context.SaveChangesAsync();

        // Documents
        var doc1 = new Document
        {
            Title = "Introduction to Machine Learning",
            FileName = "intro_ml.pdf",
            FilePath = "/uploads/intro_ml.pdf",
            ContentType = "application/pdf",
            FileSize = 2048000,
            Status = DocumentStatus.Indexed,
            CourseId = courseAI.Id,
            UploadedById = lecturer.Id
        };

        var doc2 = new Document
        {
            Title = "Neural Networks Fundamentals",
            FileName = "neural_networks.pdf",
            FilePath = "/uploads/neural_networks.pdf",
            ContentType = "application/pdf",
            FileSize = 3500000,
            Status = DocumentStatus.Indexed,
            CourseId = courseAI.Id,
            UploadedById = lecturer.Id
        };

        var doc3 = new Document
        {
            Title = "ASP.NET Core Middleware",
            FileName = "aspnet_middleware.pdf",
            FilePath = "/uploads/aspnet_middleware.pdf",
            ContentType = "application/pdf",
            FileSize = 1200000,
            Status = DocumentStatus.Pending,
            CourseId = courseWeb.Id,
            UploadedById = lecturer.Id
        };

        context.Documents.AddRange(doc1, doc2, doc3);
        await context.SaveChangesAsync();

        // Document Chunks
        context.DocumentChunks.AddRange(
            new DocumentChunk { Content = "Machine learning is a subset of artificial intelligence that enables systems to learn from data.", ChunkIndex = 0, TokenCount = 18, DocumentId = doc1.Id },
            new DocumentChunk { Content = "Supervised learning uses labeled datasets to train algorithms to classify data or predict outcomes.", ChunkIndex = 1, TokenCount = 16, DocumentId = doc1.Id },
            new DocumentChunk { Content = "Unsupervised learning identifies hidden patterns in data without pre-existing labels.", ChunkIndex = 2, TokenCount = 13, DocumentId = doc1.Id },
            new DocumentChunk { Content = "Neural networks are computing systems inspired by biological neural networks in the human brain.", ChunkIndex = 0, TokenCount = 15, DocumentId = doc2.Id },
            new DocumentChunk { Content = "Deep learning uses multiple layers of neural networks to progressively extract higher-level features.", ChunkIndex = 1, TokenCount = 16, DocumentId = doc2.Id }
        );

        // Chat Sessions & Messages
        var chatSession = new ChatSession
        {
            Title = "Understanding ML Basics",
            UserId = student.Id,
            CourseId = courseAI.Id
        };
        context.ChatSessions.Add(chatSession);
        await context.SaveChangesAsync();

        context.ChatMessages.AddRange(
            new ChatMessage { Content = "What is the difference between supervised and unsupervised learning?", IsFromUser = true, ChatSessionId = chatSession.Id },
            new ChatMessage { Content = "Supervised learning uses labeled data to train models, where each input has a corresponding correct output. Unsupervised learning finds hidden patterns in data without any predefined labels.", IsFromUser = false, SourceReferences = "intro_ml.pdf (chunks 1-2)", ChatSessionId = chatSession.Id },
            new ChatMessage { Content = "Can you give me an example of each?", IsFromUser = true, ChatSessionId = chatSession.Id },
            new ChatMessage { Content = "Supervised: email spam classification where emails are labeled as spam or not-spam. Unsupervised: customer segmentation where the algorithm groups customers by purchasing behavior without predefined categories.", IsFromUser = false, ChatSessionId = chatSession.Id }
        );

        // Quizzes & Questions
        var quiz = new Quiz
        {
            Title = "AI Fundamentals Quiz",
            Description = "Test your knowledge of basic AI and machine learning concepts.",
            TotalQuestions = 5,
            DurationMinutes = 15,
            CourseId = courseAI.Id,
            CreatedById = lecturer.Id
        };
        context.Quizzes.Add(quiz);
        await context.SaveChangesAsync();

        context.Questions.AddRange(
            new Question
            {
                Content = "Which type of learning uses labeled datasets?",
                OptionA = "Supervised Learning",
                OptionB = "Unsupervised Learning",
                OptionC = "Reinforcement Learning",
                OptionD = "Transfer Learning",
                CorrectAnswer = "A",
                Explanation = "Supervised learning requires labeled data where each input has a corresponding output.",
                OrderIndex = 0,
                QuizId = quiz.Id
            },
            new Question
            {
                Content = "What is a neural network inspired by?",
                OptionA = "Computer circuits",
                OptionB = "Biological neural networks",
                OptionC = "Mathematical equations",
                OptionD = "Database systems",
                CorrectAnswer = "B",
                Explanation = "Neural networks are computing systems inspired by biological neural networks in the human brain.",
                OrderIndex = 1,
                QuizId = quiz.Id
            },
            new Question
            {
                Content = "Which technique uses multiple layers to extract higher-level features?",
                OptionA = "Linear Regression",
                OptionB = "Decision Trees",
                OptionC = "Deep Learning",
                OptionD = "K-Means Clustering",
                CorrectAnswer = "C",
                Explanation = "Deep learning uses multiple layers of neural networks to progressively extract higher-level features.",
                OrderIndex = 2,
                QuizId = quiz.Id
            },
            new Question
            {
                Content = "Customer segmentation without predefined categories is an example of:",
                OptionA = "Supervised Learning",
                OptionB = "Unsupervised Learning",
                OptionC = "Semi-supervised Learning",
                OptionD = "Active Learning",
                CorrectAnswer = "B",
                OrderIndex = 3,
                QuizId = quiz.Id
            },
            new Question
            {
                Content = "What does NLP stand for in AI?",
                OptionA = "Neural Logic Programming",
                OptionB = "Natural Language Processing",
                OptionC = "Network Layer Protocol",
                OptionD = "Non-Linear Prediction",
                CorrectAnswer = "B",
                Explanation = "NLP stands for Natural Language Processing, a branch of AI dealing with human language.",
                OrderIndex = 4,
                QuizId = quiz.Id
            }
        );

        // Quiz Attempts
        context.QuizAttempts.AddRange(
            new QuizAttempt
            {
                Score = 4,
                TotalQuestions = 5,
                StartedAt = DateTime.UtcNow.AddDays(-2),
                CompletedAt = DateTime.UtcNow.AddDays(-2).AddMinutes(10),
                Answers = "[\"A\",\"B\",\"C\",\"B\",\"A\"]",
                QuizId = quiz.Id,
                UserId = student.Id
            }
        );

        // Payments
        context.Payments.AddRange(
            new Payment
            {
                Amount = 99000,
                Status = PaymentStatus.Completed,
                TransactionId = "TXN-2024-001",
                PaidAt = DateTime.UtcNow.AddDays(-10),
                ExpiresAt = DateTime.UtcNow.AddDays(20),
                UserId = student.Id,
                PackageId = proPkg.Id
            },
            new Payment
            {
                Amount = 199000,
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UserId = student.Id,
                PackageId = ultraPkg.Id
            }
        );

        await context.SaveChangesAsync();
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}
