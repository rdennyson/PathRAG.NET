using MediatR;
using PathRAG.Core.Models;

namespace PathRAG.Core.Queries;

public class GetAssistantByIdQuery : IRequest<Assistant?>
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
}
