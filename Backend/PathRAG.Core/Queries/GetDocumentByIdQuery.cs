using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Queries;

public class GetDocumentByIdQuery : IRequest<DocumentDto?>
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class GetDocumentByIdHandler : IRequestHandler<GetDocumentByIdQuery, DocumentDto?>
{
    private readonly PathRagDbContext _dbContext;
    private readonly ILogger<GetDocumentByIdHandler> _logger;

    public GetDocumentByIdHandler(
        PathRagDbContext dbContext,
        ILogger<GetDocumentByIdHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<DocumentDto?> Handle(GetDocumentByIdQuery request, CancellationToken cancellationToken)
    {
        // Find document chunks with the given document ID
        var chunks = await _dbContext.TextChunks
            .Where(tc => tc.FullDocumentId == request.Id.ToString())
            .OrderBy(tc => tc.ChunkOrderIndex)
            .ToListAsync(cancellationToken);

        if (!chunks.Any())
        {
            _logger.LogWarning("Document with ID {DocumentId} not found", request.Id);
            return null;
        }

        // Verify the user has access to the vector store
        var vectorStore = await _dbContext.VectorStores
            .FirstOrDefaultAsync(vs => vs.Id == chunks[0].VectorStoreId && vs.UserId == request.UserId, cancellationToken);

        if (vectorStore == null)
        {
            _logger.LogWarning("User {UserId} does not have access to vector store {VectorStoreId}",
                request.UserId, chunks[0].VectorStoreId);
            return null;
        }

        // Get entities and relationships for this document
        var entityCount = await _dbContext.Entities
            .CountAsync(e => e.SourceId == request.Id.ToString(), cancellationToken);

        var relationshipCount = await _dbContext.Relationships
            .CountAsync(r => r.SourceId == request.Id.ToString(), cancellationToken);

        // Create document DTO
        var firstChunk = chunks.First();
        return new DocumentDto
        {
            Id = Guid.Parse(firstChunk.FullDocumentId),
            Name = GetDocumentName(firstChunk.Content),
            Size = chunks.Sum(c => c.Content.Length),
            Type = "text/plain", // Default type
            VectorStoreId = firstChunk.VectorStoreId,
            UploadedAt = firstChunk.CreatedAt,
            ChunkCount = chunks.Count,
            EntityCount = entityCount,
            RelationshipCount = relationshipCount
        };
    }

    private string GetDocumentName(string content)
    {
        // Extract a title from the first few words of content
        var firstLine = content.Split('\n').FirstOrDefault() ?? "";
        var words = firstLine.Split(' ');
        var title = string.Join(" ", words.Take(Math.Min(5, words.Length)));
        
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Untitled Document";
        }
        
        return title.Length > 50 ? title.Substring(0, 47) + "..." : title;
    }
}
