using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PathRAG.Core.Models;
using System.Text;
using Npgsql;

namespace PathRAG.Infrastructure.Data;

public class PathRagDbContext : DbContext
{
    public PathRagDbContext(DbContextOptions<PathRagDbContext> options) : base(options)
    {
        // Register the vector extension with Npgsql
        // NpgsqlConnection.GlobalTypeMapper.EnableRecordsAsTuples = true;
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