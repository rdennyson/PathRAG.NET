using MediatR;
using PathRAG.Core.Models;

namespace PathRAG.Core.Commands;

public class UpdateAssistantCommand : IRequest<Assistant>
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Message { get; set; }
    public float? Temperature { get; set; }
    public List<Guid>? VectorStoreIds { get; set; }
    public string UserId { get; set; } = string.Empty;
}
