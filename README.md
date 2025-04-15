# PathRAG .NET Implementation

A .NET implementation of PathRAG (Path-based Retrieval Augmented Generation), providing powerful RAG capabilities through vector search, graph-based knowledge representation, and large language models.

## 🌟 Features

- **Hybrid Search**: Combines vector similarity search with keyword-based search for better retrieval
- **Knowledge Graph Integration**: Represents relationships between entities in a graph structure
- **Entity Extraction**: Automatically identifies entities and their relationships from documents
- **Document Chunking**: Intelligently splits documents into manageable chunks for processing
- **Vector Embeddings**: Uses state-of-the-art embedding models for semantic understanding
- **Caching**: Implements efficient caching for embeddings and LLM responses
- **PostgreSQL Integration**: Uses pgvector for efficient vector similarity search
- **Apache AGE Integration**: Leverages PostgreSQL's graph extension for graph operations

## 📋 Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL 15+](https://www.postgresql.org/download/) with [pgvector](https://github.com/pgvector/pgvector) extension
- [Apache AGE](https://age.apache.org/) extension for PostgreSQL
- Azure OpenAI API access

## 🚀 Getting Started

### 1. Set Up PostgreSQL with Extensions

Install PostgreSQL and the required extensions:

```sql
-- Install pgvector extension
CREATE EXTENSION vector;

-- Install pg_trgm extension for text search
CREATE EXTENSION pg_trgm;

-- Install Apache AGE extension
CREATE EXTENSION age;
```

### 2. Configure the Application

Update the `appsettings.json` file in the `src/PathRAG.Api` directory with your settings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=pathrag;Username=your_username;Password=your_password"
  },
  "PathRAG": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-azure-openai-api-key",
    "ApiVersion": "2023-12-01-preview",
    "DeploymentName": "your-gpt-deployment-name",
    "EmbeddingDeployment": "your-embedding-deployment-name",
    
    "CompletionModel": "gpt-4",
    "EmbeddingModel": "text-embedding-3-large",
    "KeywordExtractionModel": "gpt-4",
    
    "WorkingDirectory": "./data",
    "ChunkSize": 1200,
    "ChunkOverlap": 100,
    "MaxTokens": 32768,
    "Temperature": 0.7,
    "TopK": 40
  }
}
```

### 3. Build and Run the Application

```bash
# Navigate to the API project
cd src/PathRAG.Api

# Build the project
dotnet build

# Run the application
dotnet run
```

The API will be available at `https://localhost:7001` and `http://localhost:5001` by default.

## 📄 API Usage

### Insert a Document

```http
POST /api/Document
Content-Type: application/json

"Your document text here..."
```

### Query the System

```http
POST /api/Document/query
Content-Type: application/json

"Your question here..."
```

## 🧩 Project Structure

- **PathRAG.Api**: Web API project that exposes endpoints for document insertion and querying
- **PathRAG.Core**: Core business logic, including document processing, entity extraction, and query handling
- **PathRAG.Infrastructure**: Data access layer, including database context and storage implementations

## 🛠️ Technical Implementation Details

### Vector Search

The implementation uses PostgreSQL with the pgvector extension for efficient vector similarity search:

```csharp
// Use pgvector's cosine distance operator <=> for similarity search
var sql = $@"SELECT * FROM ""{tableName}"" 
           ORDER BY embedding <=> '{embeddingStr}'::vector 
           LIMIT {topK}";
```

### Graph Storage

Graph operations are implemented using Apache AGE, a PostgreSQL extension for graph database functionality:

```csharp
// Example of creating a relationship in the graph
var query = $"MATCH (a:Entity {{id: '{relationship.SourceEntityId}'}}), (b: {{id: '{relationship.TargetEntityId}'}}) CREATE (a)-[r:{relationship.Type} {{description: '{relationship.Description}'}}]->(b)";
```

### Document Chunking

Documents are chunked using SharpToken for accurate tokenization based on the OpenAI tokenizer:

```csharp
// Encode the content into tokens
var tokens = _encoding.Encode(content);

// Calculate chunk boundaries with overlap
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
}
```

## 🔄 Hybrid Search Implementation

The system combines vector similarity search with keyword-based search for better retrieval:

```csharp
// Semantic search using embeddings with pgvector
var semanticChunks = await PerformVectorSearchAsync(
    "TextChunks", 
    queryEmbedding, 
    _options.TopK / 2, 
    cancellationToken);

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
```

## 📚 Additional Resources

- [Azure OpenAI Documentation](https://learn.microsoft.com/en-us/azure/cognitive-services/openai/)
- [pgvector Documentation](https://github.com/pgvector/pgvector)
- [Apache AGE Documentation](https://age.apache.org/age-manual/master/index.html)
- [Entity Framework Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
