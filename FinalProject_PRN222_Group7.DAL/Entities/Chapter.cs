using System;
using System.Collections.Generic;

namespace FinalProject_PRN222_Group7.DAL.Entities
{
    public class Chapter
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int OrderIndex { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // FK
        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;

        // Navigation
        public ICollection<Document> Documents { get; set; } = new List<Document>();
        public ICollection<QuestionBankItem> QuestionBankItems { get; set; } = new List<QuestionBankItem>();
    }
}
