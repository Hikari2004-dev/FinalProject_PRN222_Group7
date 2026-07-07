namespace FinalProject_PRN222_Group7.DAL.Entities;

public class DocumentChunk
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int TokenCount { get; set; }
    public string? EmbeddingVector { get; set; }

    public int DocumentId { get; set; }
    public Document Document { get; set; } = null!;
}
