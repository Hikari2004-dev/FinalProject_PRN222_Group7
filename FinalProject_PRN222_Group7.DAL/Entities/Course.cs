namespace FinalProject_PRN222_Group7.DAL.Entities
{
    public class Course
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Code { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // FK
        public string LecturerId { get; set; } = string.Empty;
        public AppUser Lecturer { get; set; } = null!;

        // Navigation
        public ICollection<Document> Documents { get; set; } = new List<Document>();
        public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
        public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
    }
}
