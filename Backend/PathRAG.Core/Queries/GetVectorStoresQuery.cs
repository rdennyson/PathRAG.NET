using MediatR;
using PathRAG.Core.Models;

namespace PathRAG.Core.Queries;

public class GetVectorStoresQuery : IRequest<IEnumerable<VectorStore>>
{
    public string UserId { get; set; } = string.Empty;
}
