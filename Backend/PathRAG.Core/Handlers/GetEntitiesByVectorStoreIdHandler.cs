using MediatR;
using Microsoft.EntityFrameworkCore;
using PathRAG.Core.Models;
using PathRAG.Core.Queries;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Handlers;

public class GetEntitiesByVectorStoreIdHandler : IRequestHandler<GetEntitiesByVectorStoreIdQuery, IEnumerable<GraphEntity>>
{
    private readonly PathRagDbContext _dbContext;

    public GetEntitiesByVectorStoreIdHandler(PathRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<GraphEntity>> Handle(GetEntitiesByVectorStoreIdQuery request, CancellationToken cancellationToken)
    {
        // First verify that the vector store exists and belongs to the user
        var vectorStore = await _dbContext.VectorStores
            .FirstOrDefaultAsync(vs => vs.Id == request.VectorStoreId && vs.UserId == request.UserId, cancellationToken);
            
        if (vectorStore == null)
        {
            return Enumerable.Empty<GraphEntity>();
        }
        
        return await _dbContext.Entities
            .Where(e => e.VectorStoreId == request.VectorStoreId)
            .ToListAsync(cancellationToken);
    }
}
