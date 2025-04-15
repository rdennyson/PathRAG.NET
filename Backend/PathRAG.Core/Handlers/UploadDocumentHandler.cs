using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PathRAG.Core.Commands;
using PathRAG.Core.Models;
using PathRAG.Core.Services;
using PathRAG.Core.Services.Embedding;
using PathRAG.Core.Services.Entity;
using PathRAG.Infrastructure.Data;
using System.Text;

namespace PathRAG.Core.Handlers;

public class UploadDocumentHandler : IRequestHandler<UploadDocumentCommand, TextChunk[]>
{
    private readonly PathRagDbContext _dbContext;
    private readonly ITextChunkService _textChunkService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IEntityExtractionService _entityExtractionService;
    private readonly IRelationshipService _relationshipService;
    private readonly ILogger<UploadDocumentHandler> _logger;

    public UploadDocumentHandler(
        PathRagDbContext dbContext,
        ITextChunkService textChunkService,
        IEmbeddingService embeddingService,
        IEntityExtractionService entityExtractionService,
        IRelationshipService relationshipService,
        ILogger<UploadDocumentHandler> logger)
    {
        _dbContext = dbContext;
        _textChunkService = textChunkService;
        _embeddingService = embeddingService;
        _entityExtractionService = entityExtractionService;
        _relationshipService = relationshipService;
        _logger = logger;
    }

    public async Task<TextChunk[]> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
    {
        // Verify vector store exists and belongs to user
        var vectorStore = await _dbContext.VectorStores
            .FirstOrDefaultAsync(vs => vs.Id == request.VectorStoreId && vs.UserId == request.UserId, cancellationToken);

        if (vectorStore == null)
        {
            throw new KeyNotFoundException($"Vector store with ID {request.VectorStoreId} not found");
        }

        // Read file content
        string content;
        using (var reader = new StreamReader(request.File.OpenReadStream(), Encoding.UTF8))
        {
            content = await reader.ReadToEndAsync();
        }

        // Generate document ID
        var documentId = Guid.NewGuid().ToString();

        // Process document
        var chunks = _textChunkService.ChunkDocument(content);
        
        // Get embeddings for chunks
        var chunkTexts = chunks.Select(c => c.Content).ToList();
        var embeddings = await _embeddingService.GetEmbeddingsAsync(chunkTexts, cancellationToken);

        // Extract entities and relationships
        var extractionResults = await _entityExtractionService.ExtractEntitiesAndRelationshipsAsync(content, cancellationToken);
        var entities = extractionResults.Entities;
        
        // Get entity embeddings
        var entityTexts = entities.Select(e => $"{e.Name} {e.Description}").ToList();
        var entityEmbeddings = await _embeddingService.GetEmbeddingsAsync(entityTexts, cancellationToken);
        
        // Extract relationships
        var relationships = await _relationshipService.ExtractRelationshipsAsync(entities, content, cancellationToken);
        
        // Get relationship embeddings
        var relationshipTexts = relationships.Select(r => r.Description).ToList();
        var relationshipEmbeddings = await _embeddingService.GetEmbeddingsAsync(relationshipTexts, cancellationToken);

        // Save chunks to database
        var textChunks = new List<TextChunk>();
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = new TextChunk
            {
                Id = Guid.NewGuid(),
                Content = chunks[i].Content,
                Embedding = embeddings[i],
                TokenCount = chunks[i].TokenCount,
                FullDocumentId = documentId,
                ChunkOrderIndex = chunks[i].ChunkOrderIndex,
                VectorStoreId = vectorStore.Id,
                CreatedAt = DateTime.UtcNow
            };
            
            textChunks.Add(chunk);
            await _dbContext.TextChunks.AddAsync(chunk, cancellationToken);
        }

        // Save entities to database
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = new GraphEntity
            {
                Id = Guid.NewGuid(),
                Name = entities[i].Name,
                Type = entities[i].Type,
                Description = entities[i].Description,
                Embedding = entityEmbeddings[i],
                Keywords = entities[i].Keywords,
                Weight = entities[i].Weight,
                SourceId = documentId,
                VectorStoreId = vectorStore.Id,
                CreatedAt = DateTime.UtcNow
            };
            
            await _dbContext.Entities.AddAsync(entity, cancellationToken);
        }

        // Save relationships to database
        for (int i = 0; i < relationships.Count; i++)
        {
            var relationship = new Relationship
            {
                Id = Guid.NewGuid(),
                SourceEntityId = relationships[i].SourceEntityId,
                TargetEntityId = relationships[i].TargetEntityId,
                Type = relationships[i].Type,
                Description = relationships[i].Description,
                Embedding = i < relationshipEmbeddings.Count ? relationshipEmbeddings[i] : Array.Empty<float>(),
                Weight = relationships[i].Weight,
                Keywords = relationships[i].Keywords,
                SourceId = documentId,
                VectorStoreId = vectorStore.Id,
                CreatedAt = DateTime.UtcNow
            };
            
            await _dbContext.Relationships.AddAsync(relationship, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return textChunks.ToArray();
    }
}
