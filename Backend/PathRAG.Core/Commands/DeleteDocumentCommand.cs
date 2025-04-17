using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PathRAG.Core.Services.Graph;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Commands;

public class DeleteDocumentCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class DeleteDocumentHandler : IRequestHandler<DeleteDocumentCommand, bool>
{
    private readonly PathRagDbContext _dbContext;
    private readonly IGraphStorageService _graphStorage;
    private readonly ILogger<DeleteDocumentHandler> _logger;

    public DeleteDocumentHandler(
        PathRagDbContext dbContext,
        IGraphStorageService graphStorage,
        ILogger<DeleteDocumentHandler> logger)
    {
        _dbContext = dbContext;
        _graphStorage = graphStorage;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteDocumentCommand request, CancellationToken cancellationToken)
    {
        // Find document chunks with the given document ID
        var chunks = await _dbContext.TextChunks
            .Where(tc => tc.FullDocumentId == request.Id.ToString())
            .ToListAsync(cancellationToken);

        if (!chunks.Any())
        {
            _logger.LogWarning("Document with ID {DocumentId} not found", request.Id);
            return false;
        }

        // Verify the user has access to the vector store
        var vectorStoreId = chunks.First().VectorStoreId;
        var vectorStore = await _dbContext.VectorStores
            .FirstOrDefaultAsync(vs => vs.Id == vectorStoreId && vs.UserId == request.UserId, cancellationToken);

        if (vectorStore == null)
        {
            _logger.LogWarning("User {UserId} does not have access to vector store {VectorStoreId}", 
                request.UserId, vectorStoreId);
            return false;
        }

        // Find entities and relationships associated with this document
        var entities = await _dbContext.Entities
            .Where(e => e.SourceId == request.Id.ToString())
            .ToListAsync(cancellationToken);

        var entityIds = entities.Select(e => e.Id.ToString()).ToList();
        var relationships = await _dbContext.Relationships
            .Where(r => 
                entityIds.Contains(r.SourceEntityId) || 
                entityIds.Contains(r.TargetEntityId) ||
                r.SourceId == request.Id.ToString())
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Deleting document {DocumentId} with {ChunkCount} chunks, {EntityCount} entities, and {RelationshipCount} relationships",
            request.Id, chunks.Count, entities.Count, relationships.Count);

        // Remove from database
        _dbContext.Relationships.RemoveRange(relationships);
        _dbContext.Entities.RemoveRange(entities);
        _dbContext.TextChunks.RemoveRange(chunks);

        // Remove from graph storage
        await _graphStorage.RemoveEntitiesAndRelationshipsAsync(
            entities.Select(e => e.Id.ToString()).ToList(),
            relationships.Select(r => (r.SourceEntityId, r.TargetEntityId)).ToList(),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
