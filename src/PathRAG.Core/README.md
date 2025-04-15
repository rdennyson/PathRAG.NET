# PathRAG.Core

This project contains the core business logic for the PathRAG (Path-based Retrieval Augmented Generation) system, including document processing, entity extraction, embedding generation, and query handling.

## 🧩 Project Structure

### Commands

The `Commands` namespace contains MediatR command handlers for the main operations:

- `InsertDocumentCommand`: Processes and indexes a document
- `QueryDocumentCommand`: Processes a query and returns a response

### Models

The `Models` namespace contains the data models used throughout the system:

- `TextChunk`: Represents a chunk of text from a document
- `GraphEntity`: Represents an entity in the knowledge graph
- `Relationship`: Represents a relationship between entities
- `ExtractionResult`: Contains the results of entity extraction
- `PathRagOptions`: Configuration options for the system

### Services

The `Services` namespace contains the core services:

#### Embedding Services

- `IEmbeddingService`: Generates embeddings for text
- `IEntityEmbeddingService`: Generates embeddings for entities

#### Entity Services

- `IEntityExtractionService`: Extracts entities from text
- `IRelationshipService`: Manages relationships between entities

#### Graph Services

- `IGraphStorageService`: Interface for graph storage operations
- `PostgresAGEGraphStorageService`: Implementation using Apache AGE

#### Query Services

- `IHybridQueryService`: Performs hybrid search (vector + keyword + graph)
- `IKeywordExtractionService`: Extracts keywords from queries
- `IContextBuilderService`: Builds context for LLM responses

#### Cache Services

- `IEmbeddingCacheService`: Caches embeddings
- `ILLMResponseCacheService`: Caches LLM responses

## 🔍 Key Implementations

### Document Chunking

The `TextChunkService` handles document chunking using SharpToken for accurate tokenization:

```csharp
public List<TextChunk> ChunkDocument(string content)
{
    if (string.IsNullOrEmpty(content))
        return new List<TextChunk>();

    // Encode the content into tokens
    var tokens = _encoding.Encode(content);
    var results = new List<TextChunk>();

    // Calculate chunk boundaries
    for (int index = 0, start = 0; start < tokens.Count; index++, start += (_chunkSize - _chunkOverlap))
    {
        // Get the token slice for this chunk
        var end = Math.Min(start + _chunkSize, tokens.Count);
        var chunkTokens = tokens.GetRange(start, end - start);

        // Decode the tokens back to text
        var chunkContent = _encoding.Decode(chunkTokens);

        // Create the chunk
        results.Add(new TextChunk
        {
            Id = Guid.NewGuid(),
            Content = chunkContent.Trim(),
            TokenCount = chunkTokens.Count,
            ChunkOrderIndex = index,
            FullDocumentId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow
        });

        // If we've reached the end of the document, break
        if (end == tokens.Count) break;
    }

    return results;
}
```

### Entity Extraction

The `EntityExtractionService` extracts entities and relationships from text using Azure OpenAI:

```csharp
public async Task<ExtractionResult> ExtractEntitiesAndRelationshipsAsync(
    string text,
    CancellationToken cancellationToken = default)
{
    // Create a prompt for entity extraction
    var prompt = $@"Extract entities and relationships from the following text:

Text: {text}

Entities should be in the format:
- Entity Name: [Type] Description

Relationships should be in the format:
- Source Entity -> Relationship Type -> Target Entity: Description

Entities:";

    // Call Azure OpenAI to extract entities and relationships
    var response = await _openAIClient.GetCompletionsAsync(
        new CompletionsOptions(
            _options.KeywordExtractionModel,
            [prompt]
        )
        {
            MaxTokens = _options.EntitySummaryMaxTokens,
            Temperature = 0.1f
        },
        cancellationToken
    );

    // Parse the response
    var result = new ExtractionResult();
    var responseText = response.Value.Choices[0].Text;

    // Parse entities and relationships from the response
    // Implementation details...

    return result;
}
```

### Hybrid Search

The `HybridQueryService` combines vector similarity search, keyword search, and graph-based search:

```csharp
public async Task<SearchResult> SearchAsync(
    float[] queryEmbedding,
    IReadOnlyList<string> highLevelKeywords,
    IReadOnlyList<string> lowLevelKeywords,
    CancellationToken cancellationToken = default)
{
    // Semantic search using embeddings with pgvector
    var semanticChunks = await PerformVectorSearchAsync(
        "TextChunks",
        queryEmbedding,
        _options.TopK / 2,
        cancellationToken);

    // Fallback to in-memory cosine similarity if pgvector search fails
    if (semanticChunks.Count == 0)
    {
        semanticChunks = await _dbContext.TextChunks
            .OrderByDescending(c => CosineSimilarity(c.Embedding, queryEmbedding))
            .Take(_options.TopK / 2)
            .ToListAsync(cancellationToken);
    }

    // Keyword-based search
    var keywordChunks = await _dbContext.TextChunks
        .Where(c => lowLevelKeywords.Any(k => c.Content.Contains(k)))
        .ToListAsync(cancellationToken);

    // Graph-based search using high-level keywords
    var entities = new List<GraphEntity>();
    var relationships = new List<Relationship>();

    foreach (var keyword in highLevelKeywords)
    {
        var relatedNodes = await _graphStorage.GetRelatedNodesAsync(keyword);
        entities.AddRange(relatedNodes.OfType<GraphEntity>());
        relationships.AddRange(relatedNodes.OfType<Relationship>());
    }

    // Combine and deduplicate results
    var allChunks = semanticChunks.Union(keywordChunks).Distinct().ToList();

    return new SearchResult(allChunks, entities, relationships);
}
```

## 📚 Dependencies

- [MediatR](https://github.com/jbogard/MediatR): For CQRS pattern implementation
- [Azure.AI.OpenAI](https://www.nuget.org/packages/Azure.AI.OpenAI/): For Azure OpenAI API integration
- [SharpToken](https://github.com/dmitry-brazhenko/SharpToken): For tokenization
- [Microsoft.Extensions.Options](https://www.nuget.org/packages/Microsoft.Extensions.Options/): For strongly typed configuration
- [Microsoft.EntityFrameworkCore](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore/): For data access
