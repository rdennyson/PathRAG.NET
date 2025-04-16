using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PathRAG.Core.Commands;
using PathRAG.Core.Models;
using PathRAG.Core.Services;
using PathRAG.Core.Services.Embedding;
using PathRAG.Core.Services.Entity;
using PathRAG.Infrastructure.Data;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PathRAG.Core.Handlers;

public class UploadDocumentHandler : IRequestHandler<UploadDocumentCommand, TextChunk[]>
{
    private readonly PathRagDbContext _dbContext;
    private readonly ITextChunkService _textChunkService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IEntityExtractionService _entityExtractionService;
    private readonly IRelationshipService _relationshipService;
    private readonly IDocumentExtractor _documentExtractor;
    private readonly ILogger<UploadDocumentHandler> _logger;

    public UploadDocumentHandler(
        PathRagDbContext dbContext,
        ITextChunkService textChunkService,
        IEmbeddingService embeddingService,
        IEntityExtractionService entityExtractionService,
        IRelationshipService relationshipService,
        IDocumentExtractor documentExtractor,
        ILogger<UploadDocumentHandler> logger)
    {
        _dbContext = dbContext;
        _textChunkService = textChunkService;
        _embeddingService = embeddingService;
        _entityExtractionService = entityExtractionService;
        _relationshipService = relationshipService;
        _documentExtractor = documentExtractor;
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

        // Extract text from the document using the document extractor
        string content;
        try
        {
            // Get the filename
            string fileName = request.File.FileName;

            // Check if the file type is supported
            if (!_documentExtractor.IsSupported(fileName))
            {
                string fileExtension = Path.GetExtension(fileName);
                throw new NotSupportedException($"File type {fileExtension} is not supported");
            }

            // Extract text from the document
            using (var stream = request.File.OpenReadStream())
            {
                content = await _documentExtractor.ExtractTextAsync(stream, fileName, cancellationToken);
            }

            // Sanitize content to remove null bytes and other problematic characters
            content = SanitizeText(content);

            _logger.LogInformation("Successfully extracted text from {FileName} ({FileSize} bytes)",
                request.File.FileName, request.File.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from {FileName}", request.File.FileName);
            throw new InvalidOperationException($"Failed to extract text from {request.File.FileName}: {ex.Message}", ex);
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
                Content = SanitizeText(chunks[i].Content),
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
                Name = SanitizeText(entities[i].Name),
                Type = SanitizeText(entities[i].Type),
                Description = SanitizeText(entities[i].Description),
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
                Type = SanitizeText(relationships[i].Type),
                Description = SanitizeText(relationships[i].Description),
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

    private string SanitizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // Remove null bytes (0x00) which cause issues with PostgreSQL UTF-8 encoding
        text = text.Replace("\0", "");

        // Remove other control characters except for newlines and tabs
        text = Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");

        // Replace multiple whitespace characters with a single space
        text = Regex.Replace(text, @"\s+", " ");

        // Trim leading/trailing whitespace
        return text.Trim();
    }
}
