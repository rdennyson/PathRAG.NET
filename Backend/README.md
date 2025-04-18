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
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "00000000-0000-0000-0000-000000000000",
    "ClientId": "00000000-0000-0000-0000-000000000000",
    "ClientSecret": "",
    "RedirectUri": "http://localhost:3000/callback"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=pathrag;Username=postgres;Password=test123"
  },
  "PathRAG": {
    "Endpoint": "https://xyz.openai.azure.com/",
    "ApiKey": "",
    "ApiVersion": "2025-01-01-preview",
    "DeploymentName": "gpt-4o-mini",
    "EmbeddingDeployment": "text-embedding-3-large",

    "CompletionModel": "gpt-4o-mini",
    "EmbeddingModel": "text-embedding-3-large",
    "KeywordExtractionModel": "gpt-4o-mini",

    "WorkingDirectory": "./data",
    "MaxDocumentLength": 1000000,
    "EntityExtractMaxGleaning": 1,
    "EntitySummaryMaxTokens": 500,

    "ChunkSize": 1200,
    "ChunkOverlap": 100,

    "MaxTokens": 16384,
    "Temperature": 0.7,
    "TopK": 40,

    "EnableEmbeddingCache": true,
    "EnableLLMResponseCache": true,
    "CacheExpirationMinutes": 60
  }
}

```

### 3. Build and Run the Application

```bash
# Navigate to the Backend directory
cd Backend

# Build the solution
dotnet build

# Run the API project
dotnet run --project PathRAG.Api/PathRAG.Api.csproj
```

The API will be available at `http://localhost:3000` by default.

### 4. Troubleshooting Common Issues

#### Database Initialization

If you encounter database-related errors when starting the application, check the following:

1. **PostgreSQL Connection**: Ensure PostgreSQL is running and the connection string in `appsettings.json` is correct.

2. **Extensions**: Verify that the required extensions (pgvector, age, pg_trgm) are installed in your PostgreSQL database.

3. **Table Creation**: If you see errors about tables not existing, the application should automatically create them on startup. If not, you can manually run the migrations:

   ```bash
   dotnet ef database update --project PathRAG.Infrastructure
   ```

4. **Apache AGE Graph Tables**: If you encounter errors related to the Apache AGE graph tables (e.g., "relation 'ag_graph' already exists"), it might be due to conflicts between the application's table creation logic and existing tables. The application handles this by using `IF NOT EXISTS` clauses and proper error handling.

#### Authentication Issues

1. **Azure AD Configuration**: Ensure your Azure AD settings in `appsettings.json` are correct, including the client ID, tenant ID, and redirect URIs.

2. **Local Development**: For local development, you can use the included cookie authentication handler that simulates Azure AD authentication without requiring actual Azure AD credentials.

3. **CORS Issues**: If you're accessing the API from a different domain, ensure CORS is properly configured in `Program.cs`.

### 5. Verifying Installation

To verify that the application is running correctly:

1. Access the Swagger UI at `http://localhost:3000/swagger`
2. Try creating a vector store using the `/api/vectorstores` endpoint
3. Create an assistant using the `/api/assistants` endpoint
4. Upload a document to your vector store
5. Start a chat session and ask questions about your document

## 📄 API Usage

### Swagger Documentation

The API includes Swagger documentation that provides a comprehensive overview of all available endpoints. You can access it at:

```
http://localhost:3000/swagger
```

Swagger provides an interactive interface where you can:
- View all available API endpoints organized by controller
- See detailed request and response models
- Test API calls directly from the browser
- Understand authentication requirements for each endpoint

### Authentication

The application uses Azure AD authentication. To authenticate:

1. **Login Flow**:
   - Navigate to `/api/auth/login` to initiate the login process
   - You'll be redirected to the Azure AD login page
   - After successful authentication, you'll be redirected back with an authentication token
   - The token is stored as a cookie for subsequent requests

2. **Using Authentication in API Calls**:
   - All authenticated endpoints require a valid authentication cookie
   - In Swagger, once logged in, authentication is handled automatically
   - For programmatic access, include the authentication cookie in your requests

3. **Logout**:
   - To logout, call `/api/auth/logout`
   - This will invalidate your current session

### Vector Stores

#### Create a Vector Store

```http
POST /api/vectorstores
Content-Type: application/json

{
  "name": "My Knowledge Base"
}
```

Response:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "My Knowledge Base",
  "documentCount": 0,
  "createdAt": "2023-04-17T07:48:22.377Z"
}
```

#### List Vector Stores

```http
GET /api/vectorstores
```

### Assistants

#### Create an Assistant

```http
POST /api/assistants
Content-Type: application/json

{
  "name": "Research Assistant",
  "description": "Helps with research tasks",
  "instructions": "You are a helpful research assistant. Answer questions based on the provided knowledge base.",
  "vectorStoreIds": ["3fa85f64-5717-4562-b3fc-2c963f66afa6"]
}
```

Response:
```json
{
  "id": "6fa85f64-5717-4562-b3fc-2c963f66afa7",
  "name": "Research Assistant",
  "description": "Helps with research tasks",
  "instructions": "You are a helpful research assistant...",
  "createdAt": "2023-04-17T08:12:33.123Z",
  "vectorStores": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "My Knowledge Base"
    }
  ]
}
```

#### List Assistants

```http
GET /api/assistants
```

### Document Management

#### Upload a Document

```http
POST /api/vectorstores/{vectorStoreId}/documents
Content-Type: multipart/form-data

# Form data:
# file: [your file]
```

This endpoint:
- Accepts PDF, DOCX, TXT, and other document formats
- Extracts text content from the document
- Chunks the content into manageable pieces
- Creates embeddings for each chunk
- Extracts entities and relationships to build a knowledge graph
- Stores everything in the database

Response:
```json
{
  "id": "8fa85f64-5717-4562-b3fc-2c963f66afa8",
  "name": "document.pdf",
  "size": 1024000,
  "type": "application/pdf",
  "vectorStoreId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "uploadedAt": "2023-04-17T09:22:15.456Z",
  "chunkCount": 42,
  "entityCount": 18,
  "relationshipCount": 12
}
```

### Querying

#### Query an Assistant

```http
POST /api/chats/{chatId}/messages
Content-Type: application/json

{
  "content": "What are the key concepts in the uploaded documents?",
  "role": "user"
}
```

This endpoint:
- Processes your query using the selected assistant
- Retrieves relevant chunks using hybrid search (vector + keyword)
- Uses the knowledge graph to enhance context
- Generates a response using the LLM

Response:
```json
{
  "id": "9fa85f64-5717-4562-b3fc-2c963f66afa9",
  "chatSessionId": "7fa85f64-5717-4562-b3fc-2c963f66afa7",
  "content": "Based on the documents, the key concepts are...",
  "role": "assistant",
  "createdAt": "2023-04-17T09:25:33.789Z"
}
```

#### Streaming Query

For streaming responses:

```http
POST /api/query/stream
Content-Type: application/json

{
  "query": "Explain the main concepts in detail",
  "assistantId": "6fa85f64-5717-4562-b3fc-2c963f66afa7"
}
```

This returns a stream of Server-Sent Events (SSE) with partial responses as they're generated.

### Knowledge Graph

#### View Knowledge Graph

```http
GET /api/vectorstores/{vectorStoreId}/graph
```

Returns the knowledge graph for visualization:

```json
{
  "nodes": [
    {
      "id": "entity1",
      "label": "Concept A",
      "type": "concept"
    },
    {
      "id": "entity2",
      "label": "Person B",
      "type": "person"
    }
  ],
  "edges": [
    {
      "source": "entity1",
      "target": "entity2",
      "label": "created by"
    }
  ]
}
```

#### Query the Knowledge Graph

```http
POST /api/vectorstores/{vectorStoreId}/graph/query
Content-Type: application/json

{
  "query": "MATCH (n:concept)-[r]-(m) RETURN n, r, m LIMIT 10"
}
```

This allows you to run Cypher queries directly against the knowledge graph.

## 🧩 Project Structure

- **PathRAG.Api**: Web API project that exposes endpoints for document insertion and querying
- **PathRAG.Core**: Core business logic, including document processing, entity extraction, and query handling
- **PathRAG.Infrastructure**: Data access layer, including database context and storage implementations

### Database Schema

The application uses a PostgreSQL database with the following main tables:

#### Public Schema Tables

1. **VectorStores**: Stores metadata about vector stores (knowledge bases)
   - Id: Primary key (GUID)
   - Name: Vector store name
   - UserId: Owner of the vector store
   - CreatedAt: Creation timestamp
   - UpdatedAt: Last update timestamp

2. **Assistants**: Stores metadata about assistants
   - Id: Primary key (GUID)
   - Name: Assistant name
   - Description: Assistant description
   - Instructions: System instructions for the assistant
   - UserId: Owner of the assistant
   - CreatedAt: Creation timestamp
   - UpdatedAt: Last update timestamp

3. **AssistantVectorStores**: Many-to-many relationship between assistants and vector stores
   - AssistantId: Foreign key to Assistants
   - VectorStoreId: Foreign key to VectorStores

4. **TextChunks**: Stores document chunks with embeddings
   - Id: Primary key (GUID)
   - Content: Text content of the chunk
   - TokenCount: Number of tokens in the chunk
   - ChunkOrderIndex: Order of the chunk in the original document
   - FullDocumentId: ID of the original document
   - VectorStoreId: Foreign key to VectorStores
   - Embedding: Vector embedding of the chunk (pgvector type)
   - CreatedAt: Creation timestamp

5. **GraphEntities**: Stores entities extracted from documents
   - Id: Primary key (GUID)
   - Name: Entity name
   - Type: Entity type (e.g., person, organization, concept)
   - Description: Entity description
   - VectorStoreId: Foreign key to VectorStores
   - Embedding: Vector embedding of the entity (pgvector type)
   - CreatedAt: Creation timestamp

6. **Relationships**: Stores relationships between entities
   - Id: Primary key (GUID)
   - SourceEntityId: Foreign key to GraphEntities (source)
   - TargetEntityId: Foreign key to GraphEntities (target)
   - Type: Relationship type (e.g., works_for, created_by)
   - Description: Relationship description
   - VectorStoreId: Foreign key to VectorStores
   - Embedding: Vector embedding of the relationship (pgvector type)
   - CreatedAt: Creation timestamp

7. **ChatSessions**: Stores chat sessions
   - Id: Primary key (GUID)
   - Name: Chat session name
   - AssistantId: Foreign key to Assistants
   - UserId: Owner of the chat session
   - CreatedAt: Creation timestamp
   - UpdatedAt: Last update timestamp

8. **ChatMessages**: Stores messages in chat sessions
   - Id: Primary key (GUID)
   - ChatSessionId: Foreign key to ChatSessions
   - Content: Message content
   - Role: Message role (user or assistant)
   - CreatedAt: Creation timestamp

#### AG Catalog Schema Tables

These tables are managed by Apache AGE for graph operations:

1. **ag_graph**: Stores graph metadata
2. **ag_vertex**: Stores graph vertices (nodes)
3. **ag_edge**: Stores graph edges (relationships)

### Entity Relationships

The main entity relationships in the application are:

- A **VectorStore** contains many **TextChunks**, **GraphEntities**, and **Relationships**
- An **Assistant** can access multiple **VectorStores** through the **AssistantVectorStores** junction table
- A **ChatSession** belongs to one **Assistant** and contains many **ChatMessages**
- **GraphEntities** are connected to other **GraphEntities** through **Relationships**
- The graph data in the public schema is mirrored in the Apache AGE tables for graph operations

## 🛠️ Technical Implementation Details

### Vector Search

The implementation uses PostgreSQL with the pgvector extension for efficient vector similarity search:

```csharp
// Use pgvector's cosine distance operator <=> for similarity search
var sql = $@"SELECT * FROM ""{tableName}""
           ORDER BY embedding <=> '{embeddingStr}'::vector
           LIMIT {topK}";
```

### Graph Storage with Apache AGE

Graph operations are implemented using Apache AGE, a PostgreSQL extension for graph database functionality. The system uses a combination of direct SQL and Cypher queries to interact with the graph database.

#### Graph Schema

The graph database schema consists of:

1. **ag_graph**: Stores graph metadata
   - graphid: Serial primary key
   - name: Graph name (unique)
   - namespace: Namespace for the graph
   - created_at: Timestamp

2. **ag_vertex**: Stores graph vertices (nodes)
   - graph_name: Reference to ag_graph.name
   - id: Vertex ID
   - label: Vertex label (entity type)
   - properties: JSONB object containing vertex properties

3. **ag_edge**: Stores graph edges (relationships)
   - graph_name: Reference to ag_graph.name
   - start_id: Source vertex ID
   - end_id: Target vertex ID
   - label: Edge label (relationship type)
   - properties: JSONB object containing edge properties

#### Creating Vertices and Edges

```csharp
// Example of creating a vertex (entity)
var createVertexQuery = $@"
    SELECT * FROM ag_catalog.cypher('pathrag', $$
        CREATE (n:{entity.Type} {{id: '{entity.Id}', name: '{entity.Name}', description: '{entity.Description}'}})
        RETURN n
    $$) as (v agtype);"

// Example of creating an edge (relationship)
var createEdgeQuery = $@"
    SELECT * FROM ag_catalog.cypher('pathrag', $$
        MATCH (a {{id: '{relationship.SourceEntityId}'}}), (b {{id: '{relationship.TargetEntityId}'}})
        CREATE (a)-[r:{relationship.Type} {{description: '{relationship.Description}'}}]->(b)
        RETURN r
    $$) as (e agtype);"
```

#### Querying the Graph

```csharp
// Example of querying related nodes
var query = $@"
    SELECT * FROM ag_catalog.cypher('pathrag', $$
        MATCH (n {{name: '{entityName}'}})-[r]-(m)
        RETURN n, r, m
        LIMIT 10
    $$) as (results agtype);"
```

#### Graph Visualization Data

The API provides endpoints to retrieve graph data in a format suitable for visualization libraries:

```csharp
// Convert graph data to visualization format
var graphData = new {
    nodes = vertices.Select(v => new {
        id = v.Id,
        label = v.Name,
        type = v.Type
    }),
    edges = relationships.Select(r => new {
        source = r.SourceEntityId,
        target = r.TargetEntityId,
        label = r.Type
    })
};
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
