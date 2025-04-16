using Microsoft.EntityFrameworkCore;
using PathRAG.Core.Models;

namespace PathRAG.Infrastructure.Data;

public class PathRagDbContext : DbContext
{
    public PathRagDbContext(DbContextOptions<PathRagDbContext> options) : base(options)
    {
        // Register the vector extension with Npgsql
        // NpgsqlConnection.GlobalTypeMapper.EnableRecordsAsTuples = true;

        // Configure Npgsql to use snake_case for database object names
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
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
        // Set all table names to lowercase for PostgreSQL compatibility
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Set table names to lowercase
            var tableName = entity.GetTableName();
            if (tableName != null)
            {
                entity.SetTableName(tableName.ToLower());
            }

            // Set column names to lowercase
            foreach (var property in entity.GetProperties())
            {
                var columnName = property.GetColumnName();
                if (columnName != null)
                {
                    property.SetColumnName(columnName.ToLower());
                }
            }

            // Set primary key names to lowercase
            foreach (var key in entity.GetKeys())
            {
                var keyName = key.GetName();
                if (keyName != null)
                {
                    key.SetName(keyName.ToLower());
                }
            }

            // Set foreign key names to lowercase
            foreach (var key in entity.GetForeignKeys())
            {
                var constraintName = key.GetConstraintName();
                if (constraintName != null)
                {
                    key.SetConstraintName(constraintName.ToLower());
                }
            }

            // Set index names to lowercase
            foreach (var index in entity.GetIndexes())
            {
                var indexName = index.GetDatabaseName();
                if (indexName != null)
                {
                    index.SetDatabaseName(indexName.ToLower());
                }
            }
        }

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
            .HasOne(tc => tc.VectorStore)
            .WithMany(vs => vs.TextChunks)
            .HasForeignKey(tc => tc.VectorStoreId)
            .OnDelete(DeleteBehavior.Cascade);

        // Explicitly ignore any shadow properties that might be causing issues
        modelBuilder.Entity<TextChunk>().Ignore("VectorStoreId1");
        modelBuilder.Entity<TextChunk>().Ignore("VectorStoreId2");

        modelBuilder.Entity<GraphEntity>()
            .HasOne(e => e.VectorStore)
            .WithMany(vs => vs.Entities)
            .HasForeignKey(e => e.VectorStoreId)
            .OnDelete(DeleteBehavior.Cascade);

        // Explicitly ignore any shadow properties that might be causing issues
        modelBuilder.Entity<GraphEntity>().Ignore("VectorStoreId1");
        modelBuilder.Entity<GraphEntity>().Ignore("VectorStoreId2");

        modelBuilder.Entity<Relationship>()
            .HasOne(r => r.VectorStore)
            .WithMany(vs => vs.Relationships)
            .HasForeignKey(r => r.VectorStoreId)
            .OnDelete(DeleteBehavior.Cascade);

        // Explicitly ignore any shadow properties that might be causing issues
        modelBuilder.Entity<Relationship>().Ignore("VectorStoreId1");
        modelBuilder.Entity<Relationship>().Ignore("VectorStoreId2");

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

    private static void ConfigureVectorEntity<T>(ModelBuilder modelBuilder) where T : class
    {
        modelBuilder.Entity<T>()
            .Property("Embedding")
            .HasColumnType("real[]")
            .HasColumnName("embedding"); // Ensure the column name is lowercase
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Enable pgvector extension if it doesn't exist
        optionsBuilder.UseNpgsql(builder =>
            builder.EnableRetryOnFailure()
                   .CommandTimeout(60)
                   // Set PostgreSQL version
                   .SetPostgresVersion(new Version(15, 0)));
    }

    public async Task EnsureExtensionAsync()
    {
        try
        {
            // Create the pgvector extension if it doesn't exist
            await Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS vector");

            // Create the Apache AGE extension if it doesn't exist
            await Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS age");

            // Create the pg_trgm extension for text search
            await Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS pg_trgm");

            Console.WriteLine("PostgreSQL extensions installed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error installing PostgreSQL extensions: {ex.Message}");
            throw; // Re-throw the exception to ensure the application fails if extensions can't be installed
        }
    }
}