# PathRAG.Infrastructure

This project contains the data access and storage implementations for the PathRAG (Path-based Retrieval Augmented Generation) system, including database context, entity configurations, and repository implementations.

## 🧩 Project Structure

### Data

The `Data` namespace contains the database context and entity configurations:

- `PathRagDbContext`: The main database context for the application
- `Models`: Contains the database entity models

## 🔍 Key Implementations

### Database Context

The `PathRagDbContext` class configures the database context with support for vector embeddings using pgvector:

```csharp
public class PathRagDbContext : DbContext
{
    public PathRagDbContext(DbContextOptions<PathRagDbContext> options) : base(options)
    {
        // Register the vector extension with Npgsql
        NpgsqlConnection.GlobalTypeMapper.EnableRecordsAsTuples = true;
    }

    public DbSet<TextChunk> TextChunks { get; set; }
    public DbSet<GraphEntity> Entities { get; set; }
    public DbSet<Relationship> Relationships { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure vector columns
        ConfigureVectorEntity<TextChunk>(modelBuilder);
        ConfigureVectorEntity<GraphEntity>(modelBuilder);
        ConfigureVectorEntity<Relationship>(modelBuilder);
        
        // Add indexes for better performance
        modelBuilder.Entity<TextChunk>()
            .HasIndex(e => e.Content)
            .HasMethod("GIN")
            .HasOperators("gin_trgm_ops");
            
        modelBuilder.Entity<GraphEntity>()
            .HasIndex(e => e.Name);
            
        modelBuilder.Entity<GraphEntity>()
            .HasIndex(e => e.Type);
            
        modelBuilder.Entity<Relationship>()
            .HasIndex(e => new { e.SourceEntityId, e.TargetEntityId });
    }
    
    private void ConfigureVectorEntity<T>(ModelBuilder modelBuilder) where T : class
    {
        modelBuilder.Entity<T>()
            .Property("Embedding")
            .HasColumnType("vector(1536)");
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        
        // Enable pgvector extension if it doesn't exist
        optionsBuilder.UseNpgsql(builder => 
            builder.EnableRetryOnFailure()
                   .CommandTimeout(60));
    }
    
    public async Task EnsureVectorExtensionAsync()
    {
        // Create the pgvector extension if it doesn't exist
        await Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS vector");
        
        // Create the pg_trgm extension for text search
        await Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS pg_trgm");
    }
}
```

### Entity Models

The infrastructure project defines the database entity models:

#### TextChunk

```csharp
public class TextChunk
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public int TokenCount { get; set; }
    public string FullDocumentId { get; set; } = string.Empty;
    public int ChunkOrderIndex { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

#### GraphEntity

```csharp
public class GraphEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public List<string> Keywords { get; set; } = new();
    public float Weight { get; set; }
    public string SourceId { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

#### Relationship

```csharp
public class Relationship
{
    public Guid Id { get; set; }
    public string SourceEntityId { get; set; }
    public string TargetEntityId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public float Weight { get; set; }
    public List<string> Keywords { get; set; } = new();
    public string SourceId { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

## 📚 Dependencies

- [Microsoft.EntityFrameworkCore](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore/): For data access
- [Npgsql.EntityFrameworkCore.PostgreSQL](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/): For PostgreSQL support
- [ApacheAGE](https://www.nuget.org/packages/ApacheAGE/): For Apache AGE graph database support

## 🔧 PostgreSQL Extensions

The project relies on the following PostgreSQL extensions:

- [pgvector](https://github.com/pgvector/pgvector): For vector similarity search
- [pg_trgm](https://www.postgresql.org/docs/current/pgtrgm.html): For text similarity search
- [Apache AGE](https://age.apache.org/): For graph database functionality

## 🚀 Getting Started

To use this project, you need to:

1. Install PostgreSQL with the required extensions
2. Configure the connection string in `appsettings.json`
3. Ensure the database is created and initialized

```csharp
// Example of initializing the database
var dbContext = serviceProvider.GetRequiredService<PathRagDbContext>();
await dbContext.Database.EnsureCreatedAsync();
await dbContext.EnsureVectorExtensionAsync();
```
