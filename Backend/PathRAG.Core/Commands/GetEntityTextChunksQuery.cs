using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Data;
using PathRAG.Infrastructure.Models;

namespace PathRAG.Core.Commands;

public class GetEntityTextChunksQuery : IRequest<IEnumerable<TextChunk>>
{
    public Guid EntityId { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class GetEntityTextChunksHandler : IRequestHandler<GetEntityTextChunksQuery, IEnumerable<TextChunk>>
{
    private readonly PathRagDbContext _dbContext;
    private readonly ILogger<GetEntityTextChunksHandler> _logger;

    public GetEntityTextChunksHandler(
        PathRagDbContext dbContext,
        ILogger<GetEntityTextChunksHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IEnumerable<TextChunk>> Handle(
        GetEntityTextChunksQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting text chunks for entity: {EntityId}", request.EntityId);

        // Get the entity
        var entity = await _dbContext.Entities
            .FirstOrDefaultAsync(e => e.Id == request.EntityId, cancellationToken);

        if (entity == null)
        {
            throw new KeyNotFoundException($"Entity with ID {request.EntityId} not found");
        }

        // Verify vector store belongs to user
        var vectorStore = await _dbContext.VectorStores
            .FirstOrDefaultAsync(vs => vs.Id == entity.VectorStoreId && vs.UserId == request.UserId, cancellationToken);

        if (vectorStore == null)
        {
            throw new KeyNotFoundException($"Vector store not found or not accessible");
        }

        // Get text chunks that mention this entity
        // This is a simplified approach - in a real implementation, you would have a more sophisticated
        // way to link entities to the text chunks they appear in
        var textChunks = await _dbContext.TextChunks
            .Where(tc => tc.VectorStoreId == entity.VectorStoreId && tc.Content.Contains(entity.Name))
            .Select(tc => new TextChunk
            {
                Id = tc.Id,
                Content = tc.Content,
                FullDocumentId = tc.FullDocumentId,
                VectorStoreId = tc.VectorStoreId,
                CreatedAt = tc.CreatedAt
            })
            .Take(10) // Limit to 10 chunks for performance
            .ToListAsync(cancellationToken);

        return textChunks;
    }
}
