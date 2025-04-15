using MediatR;
using PathRAG.Core.Models;

namespace PathRAG.Core.Commands;

public enum SearchMode
{
    Semantic,
    Hybrid,
    Graph
}

public class QueryCommand : IRequest<QueryResult>
{
    public string Query { get; set; } = string.Empty;
    public List<Guid> VectorStoreIds { get; set; } = new List<Guid>();
    public SearchMode SearchMode { get; set; } = SearchMode.Hybrid;
    public Guid AssistantId { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class QueryResult
{
    public string Answer { get; set; } = string.Empty;
    public List<string> Sources { get; set; } = new List<string>();
    public List<GraphEntity>? Entities { get; set; }
    public List<Relationship>? Relationships { get; set; }
}
