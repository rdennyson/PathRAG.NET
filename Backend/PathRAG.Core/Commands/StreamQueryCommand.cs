using MediatR;
using PathRAG.Core.Models;

namespace PathRAG.Core.Commands;

public class StreamQueryCommand : IRequest<IAsyncEnumerable<string>>
{
    public string Query { get; set; } = string.Empty;
    public List<Guid> VectorStoreIds { get; set; } = new List<Guid>();
    public SearchMode SearchMode { get; set; } = SearchMode.Hybrid;
    public Guid AssistantId { get; set; }
    public string UserId { get; set; } = string.Empty;
}
