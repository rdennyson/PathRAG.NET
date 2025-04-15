using MediatR;
using PathRAG.Core.Models;

namespace PathRAG.Core.Queries;

public class GetRelationshipsByVectorStoreIdQuery : IRequest<IEnumerable<Relationship>>
{
    public Guid VectorStoreId { get; set; }
    public string UserId { get; set; } = string.Empty;
}
