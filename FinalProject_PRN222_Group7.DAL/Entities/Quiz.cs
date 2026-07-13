namespace FinalProject_PRN222_Group7.DAL.Entities
{
    public class Quiz
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int TotalQuestions { get; set; }
        public int TimeLimit { get; set; } // minutes, 0 = no limit
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsAiGenerated { get; set; } = true;

        // FK
        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;
        public int? DocumentId { get; set; }
        public Document? Document { get; set; }

        // Navigation
        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<QuizAttempt> Attempts { get; set; } = new List<QuizAttempt>();
    }

    public class Question
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public string OptionA { get; set; } = string.Empty;
        public string OptionB { get; set; } = string.Empty;
        public string OptionC { get; set; } = string.Empty;
        public string OptionD { get; set; } = string.Empty;
        public char CorrectAnswer { get; set; } // A, B, C, D
        public string? Explanation { get; set; }
        public int OrderIndex { get; set; }

        // FK
        public int QuizId { get; set; }
        public Quiz Quiz { get; set; } = null!;
    }

    public class QuizAttempt
    {
        public int Id { get; set; }
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public string? AnswersJson { get; set; } // JSON: {questionId: selectedOption}
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public bool IsCompleted { get; set; }

        // FK
        public int QuizId { get; set; }
        public Quiz Quiz { get; set; } = null!;
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
    }
}
