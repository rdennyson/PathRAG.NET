namespace PathRAG.Core.Models;

public class AssistantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public float Temperature { get; set; }
    public List<Guid> VectorStoreIds { get; set; } = new List<Guid>();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateAssistantRequest
{
    public string Name { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public float Temperature { get; set; } = 0.7f;
    public List<Guid> VectorStoreIds { get; set; } = new List<Guid>();
}

public class UpdateAssistantRequest
{
    public string? Name { get; set; }
    public string? Message { get; set; }
    public float? Temperature { get; set; }
    public List<Guid>? VectorStoreIds { get; set; }
}
