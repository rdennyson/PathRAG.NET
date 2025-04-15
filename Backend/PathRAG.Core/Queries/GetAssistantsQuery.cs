using MediatR;
using PathRAG.Core.Models;

namespace PathRAG.Core.Queries;

public class GetAssistantsQuery : IRequest<IEnumerable<Assistant>>
{
    public string UserId { get; set; } = string.Empty;
}
