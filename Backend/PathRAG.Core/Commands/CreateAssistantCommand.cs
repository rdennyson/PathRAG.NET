using MediatR;
using PathRAG.Core.Models;

namespace PathRAG.Core.Commands;

public class CreateAssistantCommand : IRequest<Assistant>
{
    public string Name { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public float Temperature { get; set; } = 0.7f;
    public List<Guid> VectorStoreIds { get; set; } = new List<Guid>();
    public string UserId { get; set; } = string.Empty;
}
