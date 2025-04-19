namespace PathRAG.Core.Models;

public class VectorStoreDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DocumentCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateVectorStoreRequest
{
    public string Name { get; set; } = string.Empty;
}

public class DocumentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Type { get; set; } = string.Empty;
    public Guid VectorStoreId { get; set; }
    public DateTime UploadedAt { get; set; }
    public int ChunkCount { get; set; }
    public int EntityCount { get; set; }
    public int RelationshipCount { get; set; }
}


