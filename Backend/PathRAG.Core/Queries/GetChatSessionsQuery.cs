using MediatR;
using PathRAG.Core.Models;

namespace PathRAG.Core.Queries;

public class GetChatSessionsQuery : IRequest<IEnumerable<ChatSession>>
{
    public string UserId { get; set; } = string.Empty;
}
