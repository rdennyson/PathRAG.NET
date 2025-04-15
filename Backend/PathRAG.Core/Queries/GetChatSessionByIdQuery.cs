using MediatR;
using PathRAG.Core.Models;

namespace PathRAG.Core.Queries;

public class GetChatSessionByIdQuery : IRequest<ChatSession?>
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
}
