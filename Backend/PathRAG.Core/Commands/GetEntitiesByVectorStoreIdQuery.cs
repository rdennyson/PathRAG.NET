using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Commands;

public class GetEntitiesByVectorStoreIdQuery : IRequest<IEnumerable<GraphEntityDto>>
{
    public Guid VectorStoreId { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class GetEntitiesByVectorStoreIdHandler : IRequestHandler<GetEntitiesByVectorStoreIdQuery, IEnumerable<GraphEntityDto>>
{
    private readonly PathRagDbContext _dbContext;
    private readonly ILogger<GetEntitiesByVectorStoreIdHandler> _logger;

    public GetEntitiesByVectorStoreIdHandler(
        PathRagDbContext dbContext,
        ILogger<GetEntitiesByVectorStoreIdHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IEnumerable<GraphEntityDto>> Handle(
        GetEntitiesByVectorStoreIdQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting entities for vector store: {VectorStoreId}", request.VectorStoreId);

        // Verify vector store exists and belongs to user
        var vectorStore = await _dbContext.VectorStores
            .FirstOrDefaultAsync(vs => vs.Id == request.VectorStoreId && vs.UserId == request.UserId, cancellationToken);

        if (vectorStore == null)
        {
            throw new KeyNotFoundException($"Vector store with ID {request.VectorStoreId} not found");
        }

        // Get entities for the vector store
        var entities = await _dbContext.Entities
            .Where(e => e.VectorStoreId == request.VectorStoreId)
            .Select(e => new GraphEntityDto
            {
                Id = e.Id,
                Name = e.Name,
                Type = e.Type,
                Description = e.Description,
                //Keywords = e.Keywords,
                //Weight = e.Weight,
                VectorStoreId = e.VectorStoreId,
                //CreatedAt = e.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return entities;
    }
}
