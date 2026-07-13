using System;

namespace FinalProject_PRN222_Group7.DAL.Entities
{
    public class QuestionBankItem
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public string OptionA { get; set; } = string.Empty;
        public string OptionB { get; set; } = string.Empty;
        public string OptionC { get; set; } = string.Empty;
        public string OptionD { get; set; } = string.Empty;
        public char CorrectAnswer { get; set; } // A, B, C, D
        public string? Explanation { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // FK Môn học
        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;

        // FK Chương/Tài liệu (tùy chọn)
        public int? DocumentId { get; set; }
        public Document? Document { get; set; }

        public int? ChapterId { get; set; }
        public Chapter? Chapter { get; set; }
    }
}
