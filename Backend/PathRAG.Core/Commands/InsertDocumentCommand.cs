using MediatR;
using PathRAG.Core.Models;
using PathRAG.Core.Services;
using PathRAG.Core.Services.Embedding;
using PathRAG.Core.Services.Entity;
using PathRAG.Core.Services.Graph;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Commands;

public record InsertDocumentCommand(string Content) : IRequest;

public class InsertDocumentCommandHandler : IRequestHandler<InsertDocumentCommand>
{
    private readonly ITextChunkService _chunkService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IEntityExtractionService _entityExtractor;
    private readonly IGraphStorageService _graphStorage;
    private readonly IEntityEmbeddingService _entityEmbeddingService;
    private readonly IRelationshipService _relationshipService;
    private readonly PathRagDbContext _dbContext;

    public InsertDocumentCommandHandler(
        ITextChunkService chunkService,
        IEmbeddingService embeddingService,
        IEntityExtractionService entityExtractor,
        IGraphStorageService graphStorage,
        IEntityEmbeddingService entityEmbeddingService,
        IRelationshipService relationshipService,
        PathRagDbContext dbContext)
    {
        _chunkService = chunkService;
        _embeddingService = embeddingService;
        _entityExtractor = entityExtractor;
        _graphStorage = graphStorage;
        _entityEmbeddingService = entityEmbeddingService;
        _relationshipService = relationshipService;
        _dbContext = dbContext;
    }

    public async Task Handle(InsertDocumentCommand request, CancellationToken cancellationToken)
    {
        // 1. Chunk the document
        var chunks = _chunkService.ChunkDocument(request.Content);

        // 2. Generate embeddings for chunks
        var chunkEmbeddings = await _embeddingService.GetEmbeddingsAsync(
            chunks.Select(c => c.Content).ToList(), 
            cancellationToken
        );

        // 3. Extract entities and relationships from chunks
        var extractionResults = await Task.WhenAll(
            chunks.Select(chunk => 
                _entityExtractor.ExtractEntitiesAndRelationshipsAsync(chunk.Content, cancellationToken)
            )
        );

        // 4. Process entities
        var entities = extractionResults.SelectMany(r => r.Entities).Distinct().ToList();
        var entityEmbeddings = await _entityEmbeddingService.GetEmbeddingsAsync(
            entities.Select(e => e.Name + " " + e.Description).ToList(),
            cancellationToken
        );

        // 5. Process relationships
        var relationships = extractionResults.SelectMany(r => r.Relationships).Distinct().ToList();
        var relationshipEmbeddings = await _entityEmbeddingService.GetEmbeddingsAsync(
            relationships.Select(r => r.Description).ToList(),
            cancellationToken
        );

        // 6. Save to database
        for (int i = 0; i < chunks.Count; i++)
        {
            _dbContext.TextChunks.Add(new TextChunk
            {
                Id = Guid.NewGuid(),
                Content = chunks[i].Content,
                Embedding = chunkEmbeddings[i],
                CreatedAt = DateTime.UtcNow
            });
        }

        for (int i = 0; i < entities.Count; i++)
        {
            _dbContext.Entities.Add(new GraphEntity
            {
                Id = Guid.NewGuid(),
                Name = entities[i].Name,
                Type = entities[i].Type,
                Description = entities[i].Description,
                Embedding = entityEmbeddings[i]
            });
        }

        for (int i = 0; i < relationships.Count; i++)
        {
            _dbContext.Relationships.Add(new Relationship
            {
                Id = Guid.NewGuid(),
                SourceEntityId = relationships[i].SourceEntityId,
                TargetEntityId = relationships[i].TargetEntityId,
                Type = relationships[i].Type,
                Description = relationships[i].Description,
                Embedding = relationshipEmbeddings[i]
            });
        }

        // 7. Update graph storage
        await _graphStorage.AddEntitiesAndRelationshipsAsync(entities, relationships, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

    }
}
