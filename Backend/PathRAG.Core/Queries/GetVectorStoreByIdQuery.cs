using MediatR;
using PathRAG.Core.Models;

namespace PathRAG.Core.Queries;

public class GetVectorStoreByIdQuery : IRequest<VectorStore?>
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
}
