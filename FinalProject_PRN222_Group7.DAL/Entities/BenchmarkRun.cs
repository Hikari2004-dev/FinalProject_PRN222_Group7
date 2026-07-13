namespace FinalProject_PRN222_Group7.DAL.Entities
{
    public class BenchmarkRun
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ChunkingStrategy { get; set; } = string.Empty; // "fixed" | "recursive"
        public int ChunkSize { get; set; }
        public int ChunkOverlap { get; set; }
        public string EmbeddingModel { get; set; } = string.Empty;
        public int TotalQuestions { get; set; }
        public double Faithfulness { get; set; }
        public double AnswerRelevancy { get; set; }
        public double ContextPrecision { get; set; }
        public double ContextRecall { get; set; }
        public double OverallAccuracy { get; set; }
        public string? ResultsJson { get; set; }
        public DateTime RunAt { get; set; } = DateTime.UtcNow;
        public string RunById { get; set; } = string.Empty;
    }
}
