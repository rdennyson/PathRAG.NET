using MediatR;
using PathRAG.Core.Models;

namespace PathRAG.Core.Queries;

public class GetEntitiesByVectorStoreIdQuery : IRequest<IEnumerable<GraphEntity>>
{
    public Guid VectorStoreId { get; set; }
    public string UserId { get; set; } = string.Empty;
}
