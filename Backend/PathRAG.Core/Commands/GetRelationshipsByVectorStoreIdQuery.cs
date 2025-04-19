using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Commands;

public class GetRelationshipsByVectorStoreIdQuery : IRequest<IEnumerable<RelationshipDto>>
{
    public Guid VectorStoreId { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class GetRelationshipsByVectorStoreIdHandler : IRequestHandler<GetRelationshipsByVectorStoreIdQuery, IEnumerable<RelationshipDto>>
{
    private readonly PathRagDbContext _dbContext;
    private readonly ILogger<GetRelationshipsByVectorStoreIdHandler> _logger;

    public GetRelationshipsByVectorStoreIdHandler(
        PathRagDbContext dbContext,
        ILogger<GetRelationshipsByVectorStoreIdHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IEnumerable<RelationshipDto>> Handle(
        GetRelationshipsByVectorStoreIdQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting relationships for vector store: {VectorStoreId}", request.VectorStoreId);

        // Verify vector store exists and belongs to user
        var vectorStore = await _dbContext.VectorStores
            .FirstOrDefaultAsync(vs => vs.Id == request.VectorStoreId && vs.UserId == request.UserId, cancellationToken);

        if (vectorStore == null)
        {
            throw new KeyNotFoundException($"Vector store with ID {request.VectorStoreId} not found");
        }

        // Get relationships for the vector store
        var relationships = await _dbContext.Relationships
            .Where(r => r.VectorStoreId == request.VectorStoreId)
            .Select(r => new RelationshipDto
            {
                Id = r.Id,
                SourceEntityId = r.SourceEntityId,
                TargetEntityId = r.TargetEntityId,
                Type = r.Type,
                Description = r.Description,
                //Weight = r.Weight,
                VectorStoreId = r.VectorStoreId,
                //CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return relationships;
    }
}
