using MediatR;
using PathRAG.Core.Models;

namespace PathRAG.Core.Commands;

public class CreateChatSessionCommand : IRequest<ChatSession>
{
    public Guid AssistantId { get; set; }
    public string UserId { get; set; } = string.Empty;
}
