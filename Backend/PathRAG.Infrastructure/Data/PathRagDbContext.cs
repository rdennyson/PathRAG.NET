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
    public DbSet<Assistant> Assistants { get; set; }
    public DbSet<VectorStore> VectorStores { get; set; }
    public DbSet<AssistantVectorStore> AssistantVectorStores { get; set; }
    public DbSet<ChatSession> ChatSessions { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<MessageAttachment> MessageAttachments { get; set; }

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

        // Configure relationships
        modelBuilder.Entity<TextChunk>()
            .HasOne<VectorStore>()
            .WithMany(vs => vs.TextChunks)
            .HasForeignKey(tc => tc.VectorStoreId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GraphEntity>()
            .HasOne<VectorStore>()
            .WithMany(vs => vs.Entities)
            .HasForeignKey(e => e.VectorStoreId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Relationship>()
            .HasOne<VectorStore>()
            .WithMany(vs => vs.Relationships)
            .HasForeignKey(r => r.VectorStoreId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AssistantVectorStore>()
            .HasKey(avs => new { avs.AssistantId, avs.VectorStoreId });

        modelBuilder.Entity<AssistantVectorStore>()
            .HasOne(avs => avs.Assistant)
            .WithMany(a => a.AssistantVectorStores)
            .HasForeignKey(avs => avs.AssistantId);

        modelBuilder.Entity<AssistantVectorStore>()
            .HasOne(avs => avs.VectorStore)
            .WithMany(vs => vs.AssistantVectorStores)
            .HasForeignKey(avs => avs.VectorStoreId);

        modelBuilder.Entity<ChatSession>()
            .HasOne(cs => cs.Assistant)
            .WithMany(a => a.ChatSessions)
            .HasForeignKey(cs => cs.AssistantId);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(cm => cm.ChatSession)
            .WithMany(cs => cs.Messages)
            .HasForeignKey(cm => cm.ChatSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MessageAttachment>()
            .HasOne(ma => ma.ChatMessage)
            .WithMany(cm => cm.Attachments)
            .HasForeignKey(ma => ma.ChatMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        // Add indexes for new entities
        modelBuilder.Entity<Assistant>()
            .HasIndex(a => a.UserId);

        modelBuilder.Entity<VectorStore>()
            .HasIndex(vs => vs.UserId);

        modelBuilder.Entity<ChatSession>()
            .HasIndex(cs => cs.UserId);

        modelBuilder.Entity<ChatSession>()
            .HasIndex(cs => cs.AssistantId);

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(cm => cm.ChatSessionId);
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