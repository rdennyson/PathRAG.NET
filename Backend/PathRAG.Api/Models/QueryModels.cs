namespace PathRAG.Api.Models;

public class QueryRequest
{
    public string Query { get; set; } = string.Empty;
    public List<Guid> VectorStoreIds { get; set; } = new List<Guid>();
    public string SearchMode { get; set; } = "Hybrid";
    public Guid AssistantId { get; set; }
}

public class QueryResponseDto
{
    public string Answer { get; set; } = string.Empty;
    public List<string> Sources { get; set; } = new List<string>();
    public List<GraphEntityDto>? Entities { get; set; }
    public List<RelationshipDto>? Relationships { get; set; }
}
