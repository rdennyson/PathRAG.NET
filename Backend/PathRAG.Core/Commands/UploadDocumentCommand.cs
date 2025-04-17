using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PathRAG.Core.Models;
using PathRAG.Core.Services;
using PathRAG.Core.Services.Embedding;
using PathRAG.Core.Services.Entity;
using PathRAG.Core.Services.Graph;
using PathRAG.Infrastructure.Data;
using PathRAG.Infrastructure.Models;
using System.Text.RegularExpressions;

namespace PathRAG.Core.Commands;

public class UploadDocumentCommand : IRequest<TextChunk[]>
{
    public Guid VectorStoreId { get; set; }
    public IFormFile File { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
}

public class UploadDocumentHandler : IRequestHandler<UploadDocumentCommand, TextChunk[]>
{
    private readonly PathRagDbContext _dbContext;
    private readonly ITextChunkService _textChunkService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IEntityExtractionService _entityExtractionService;
    private readonly IRelationshipService _relationshipService;
    private readonly IDocumentExtractor _documentExtractor;
    private readonly IGraphStorageService _graphStorage;
    private readonly IEntityEmbeddingService _entityEmbeddingService;
    private readonly ILogger<UploadDocumentHandler> _logger;

    public UploadDocumentHandler(
        PathRagDbContext dbContext,
        ITextChunkService textChunkService,
        IEmbeddingService embeddingService,
        IEntityExtractionService entityExtractionService,
        IRelationshipService relationshipService,
        IDocumentExtractor documentExtractor,
        IGraphStorageService graphStorage,
        IEntityEmbeddingService entityEmbeddingService,
        ILogger<UploadDocumentHandler> logger)
    {
        _dbContext = dbContext;
        _textChunkService = textChunkService;
        _embeddingService = embeddingService;
        _entityExtractionService = entityExtractionService;
        _relationshipService = relationshipService;
        _documentExtractor = documentExtractor;
        _graphStorage = graphStorage;
        _entityEmbeddingService = entityEmbeddingService;
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

        // Extract text from file
        string content;
        using (var stream = request.File.OpenReadStream())
        {
            content = await _documentExtractor.ExtractTextAsync(stream, request.File.FileName, cancellationToken);
        }

        _logger.LogInformation("Extracted {CharCount} characters from file {FileName}", content.Length, request.File.FileName);

        // Generate document ID
        var documentId = Guid.NewGuid().ToString();

        // Process document
        var chunks = _textChunkService.ChunkDocument(content);

        // Get embeddings for chunks
        var chunkTexts = chunks.Select(c => c.Content).ToList();
        var embeddings = await _embeddingService.GetEmbeddingsAsync(chunkTexts, cancellationToken);

        // Extract entities and relationships from chunks
        var extractionResults = await Task.WhenAll(
            chunks.Select(chunk =>
                _entityExtractionService.ExtractEntitiesAndRelationshipsAsync(chunk.Content, cancellationToken)
            )
        );

        // Process entities
        var entities = extractionResults.SelectMany(r => r.Entities).Distinct().ToList();
        var entityTexts = entities.Select(e => $"{e.Name} {e.Description}").ToList();
        var entityEmbeddings = await _entityEmbeddingService.GetEmbeddingsAsync(entityTexts, cancellationToken);

        // Process relationships
        var relationships = extractionResults.SelectMany(r => r.Relationships).Distinct().ToList();
        var relationshipTexts = relationships.Select(r => r.Description).ToList();
        var relationshipEmbeddings = await _entityEmbeddingService.GetEmbeddingsAsync(relationshipTexts, cancellationToken);

        // Save chunks to database
        var textChunks = new List<TextChunk>();
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = new TextChunk
            {
                Id = Guid.NewGuid(),
                Content = SanitizeText(chunks[i].Content),
                Embedding = new Pgvector.Vector(embeddings[i]),
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
                Name = SanitizeText(entities[i].Name),
                Type = SanitizeText(entities[i].Type),
                Description = SanitizeText(entities[i].Description),
                Embedding = new Pgvector.Vector(entityEmbeddings[i]),
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
                Type = SanitizeText(relationships[i].Type),
                Description = SanitizeText(relationships[i].Description),
                Embedding = new Pgvector.Vector(i < relationshipEmbeddings.Count ? relationshipEmbeddings[i] : Array.Empty<float>()),
                Weight = relationships[i].Weight,
                Keywords = relationships[i].Keywords,
                SourceId = documentId,
                VectorStoreId = vectorStore.Id,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.Relationships.AddAsync(relationship, cancellationToken);
        }

        // Update graph storage
        await _graphStorage.AddEntitiesAndRelationshipsAsync(entities, relationships, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return textChunks.ToArray();
    }

    private string SanitizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Replace multiple spaces with a single space
        text = Regex.Replace(text, @"\s+", " ");

        // Remove special characters that might cause issues in SQL or JSON
        text = Regex.Replace(text, @"[^\w\s.,;:!?()[\]{}\-'""–—]", "");

        // Trim the text
        return text.Trim();
    }
}
