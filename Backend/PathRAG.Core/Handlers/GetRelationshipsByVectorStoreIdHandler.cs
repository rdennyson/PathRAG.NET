using MediatR;
using Microsoft.EntityFrameworkCore;
using PathRAG.Core.Models;
using PathRAG.Core.Queries;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Handlers;

public class GetRelationshipsByVectorStoreIdHandler : IRequestHandler<GetRelationshipsByVectorStoreIdQuery, IEnumerable<Relationship>>
{
    private readonly PathRagDbContext _dbContext;

    public GetRelationshipsByVectorStoreIdHandler(PathRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<Relationship>> Handle(GetRelationshipsByVectorStoreIdQuery request, CancellationToken cancellationToken)
    {
        // First verify that the vector store exists and belongs to the user
        var vectorStore = await _dbContext.VectorStores
            .FirstOrDefaultAsync(vs => vs.Id == request.VectorStoreId && vs.UserId == request.UserId, cancellationToken);
            
        if (vectorStore == null)
        {
            return Enumerable.Empty<Relationship>();
        }
        
        return await _dbContext.Relationships
            .Where(r => r.VectorStoreId == request.VectorStoreId)
            .ToListAsync(cancellationToken);
    }
}
