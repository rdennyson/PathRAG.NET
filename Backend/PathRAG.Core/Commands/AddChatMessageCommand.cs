using MediatR;
using Microsoft.AspNetCore.Http;
using PathRAG.Core.Models;

namespace PathRAG.Core.Commands;

public class AddChatMessageCommand : IRequest<ChatMessage>
{
    public Guid ChatSessionId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public List<IFormFile>? Attachments { get; set; }
    public string UserId { get; set; } = string.Empty;
}
