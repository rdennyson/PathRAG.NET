# PathRAG.Api

This project provides a RESTful API for the PathRAG (Path-based Retrieval Augmented Generation) system, allowing you to insert documents and perform queries using the power of vector search, graph-based knowledge representation, and large language models.

## 🚀 Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL 15+](https://www.postgresql.org/download/) with [pgvector](https://github.com/pgvector/pgvector) extension
- [Apache AGE](https://age.apache.org/) extension for PostgreSQL
- Azure OpenAI API access

### Configuration

Before running the application, update the `appsettings.json` file with your settings:

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
    "TopK": 40,
    
    "EnableEmbeddingCache": true,
    "EnableLLMResponseCache": true,
    "CacheExpirationMinutes": 60
  }
}
```

### Running the Application

```bash
# Build the project
dotnet build

# Run the application
dotnet run
```

The API will be available at:
- HTTPS: `https://localhost:7001`
- HTTP: `http://localhost:5001`

## 📄 API Endpoints

### Insert a Document

Inserts a document into the system for processing and indexing.

```http
POST /api/Document
Content-Type: application/json

"Your document text here..."
```

### Query the System

Queries the system with a natural language question.

```http
POST /api/Document/query
Content-Type: application/json

"Your question here..."
```

## 🧪 Testing the API

You can test the API using tools like [Postman](https://www.postman.com/) or [curl](https://curl.se/).

### Example with curl

Insert a document:

```bash
curl -X POST "https://localhost:7001/api/Document" \
     -H "Content-Type: application/json" \
     -d "\"This is a sample document about artificial intelligence. AI is transforming many industries through automation and data analysis.\""
```

Query the system:

```bash
curl -X POST "https://localhost:7001/api/Document/query" \
     -H "Content-Type: application/json" \
     -d "\"What industries is AI transforming?\""
```

## 🔄 Database Initialization

When the application starts, it automatically:

1. Creates the database if it doesn't exist
2. Ensures the pgvector extension is installed
3. Initializes the graph storage

This is handled in the `Program.cs` file:

```csharp
// Ensure Database and Graph Storage are initialized
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PathRagDbContext>();
    var graphStorage = scope.ServiceProvider.GetRequiredService<IGraphStorageService>();

    // Create database if it doesn't exist
    await dbContext.Database.EnsureCreatedAsync();

    // Ensure vector extension is installed
    await dbContext.EnsureVectorExtensionAsync();

    // Initialize graph storage
    await graphStorage.InitializeAsync();

    app.Logger.LogInformation("Database and extensions initialized successfully");
}
```

## 🛠️ Dependency Injection

The application uses .NET's built-in dependency injection container to manage services. Key services include:

- `ITextChunkService`: Handles document chunking
- `IEmbeddingService`: Generates embeddings for text
- `IGraphStorageService`: Manages the graph database
- `IHybridQueryService`: Performs hybrid search (vector + keyword + graph)
- `IEntityExtractionService`: Extracts entities from text
- `ILLMResponseCacheService`: Caches LLM responses

## 📚 Additional Resources

- [Azure OpenAI Documentation](https://learn.microsoft.com/en-us/azure/cognitive-services/openai/)
- [ASP.NET Core Documentation](https://docs.microsoft.com/en-us/aspnet/core/)
- [Entity Framework Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
