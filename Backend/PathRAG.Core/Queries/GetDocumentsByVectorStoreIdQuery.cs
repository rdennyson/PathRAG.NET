using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Queries;

public class GetDocumentsByVectorStoreIdQuery : IRequest<IEnumerable<DocumentDto>>
{
    public Guid VectorStoreId { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class GetDocumentsByVectorStoreIdHandler : IRequestHandler<GetDocumentsByVectorStoreIdQuery, IEnumerable<DocumentDto>>
{
    private readonly PathRagDbContext _dbContext;
    private readonly ILogger<GetDocumentsByVectorStoreIdHandler> _logger;

    public GetDocumentsByVectorStoreIdHandler(
        PathRagDbContext dbContext,
        ILogger<GetDocumentsByVectorStoreIdHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IEnumerable<DocumentDto>> Handle(GetDocumentsByVectorStoreIdQuery request, CancellationToken cancellationToken)
    {
        // Verify vector store exists and belongs to user
        var vectorStore = await _dbContext.VectorStores
            .FirstOrDefaultAsync(vs => vs.Id == request.VectorStoreId && vs.UserId == request.UserId, cancellationToken);

        if (vectorStore == null)
        {
            _logger.LogWarning("Vector store {VectorStoreId} not found for user {UserId}", 
                request.VectorStoreId, request.UserId);
            throw new KeyNotFoundException($"Vector store with ID {request.VectorStoreId} not found");
        }

        // Get unique document IDs from text chunks
        var documentGroups = await _dbContext.TextChunks
            .Where(tc => tc.VectorStoreId == request.VectorStoreId)
            .GroupBy(tc => tc.FullDocumentId)
            .Select(g => new
            {
                DocumentId = g.Key,
                FirstChunk = g.OrderBy(tc => tc.ChunkOrderIndex).First(),
                ChunkCount = g.Count(),
                TotalSize = g.Sum(tc => tc.Content.Length)
            })
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {DocumentCount} documents in vector store {VectorStoreId}",
            documentGroups.Count, request.VectorStoreId);

        // Get entity and relationship counts for each document
        var documentIds = documentGroups.Select(g => g.DocumentId).ToList();
        
        var entityCounts = await _dbContext.Entities
            .Where(e => documentIds.Contains(e.SourceId))
            .GroupBy(e => e.SourceId)
            .Select(g => new { DocumentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DocumentId, x => x.Count, cancellationToken);
            
        var relationshipCounts = await _dbContext.Relationships
            .Where(r => documentIds.Contains(r.SourceId))
            .GroupBy(r => r.SourceId)
            .Select(g => new { DocumentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DocumentId, x => x.Count, cancellationToken);

        // Convert to DocumentDto objects
        var documents = documentGroups.Select(g => new DocumentDto
        {
            Id = Guid.Parse(g.DocumentId),
            Name = GetDocumentName(g.FirstChunk.Content),
            Size = g.TotalSize,
            Type = "text/plain", // Default type
            VectorStoreId = request.VectorStoreId,
            UploadedAt = g.FirstChunk.CreatedAt,
            ChunkCount = g.ChunkCount,
            EntityCount = entityCounts.TryGetValue(g.DocumentId, out var entityCount) ? entityCount : 0,
            RelationshipCount = relationshipCounts.TryGetValue(g.DocumentId, out var relationshipCount) ? relationshipCount : 0
        });

        return documents;
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
