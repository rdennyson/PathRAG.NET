using Microsoft.EntityFrameworkCore;
using Npgsql;
using PathRAG.Infrastructure.Models;
using PathRAG.Infrastructure.Models.Graph;
using Microsoft.EntityFrameworkCore.Metadata;

namespace PathRAG.Infrastructure.Data;

public class PathRagDbContext : DbContext
{
    static PathRagDbContext()
    {
        // Configure Npgsql to use snake_case for database object names
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public PathRagDbContext(DbContextOptions<PathRagDbContext> options) : base(options)
    {
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

    // Apache AGE graph tables
    public DbSet<AGGraph> AGGraphs { get; set; }
    public DbSet<AGVertex> AGVertices { get; set; }
    public DbSet<AGEdge> AGEdges { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Add extensions support
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("age");
        modelBuilder.HasPostgresExtension("pg_trgm");

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

        // Configure Apache AGE graph tables
        modelBuilder.Entity<AGGraph>(eb =>
        {
            eb.ToTable(
                name: "ag_graph",
                schema: "ag_catalog",
                buildAction: tb => tb.ExcludeFromMigrations()
            );

            eb.HasKey(e => e.Name);
        });
        modelBuilder.Entity<AGVertex>()
            .HasKey(v => new { v.GraphName, v.Id });

        modelBuilder.Entity<AGVertex>()
            .HasOne(v => v.Graph)
            .WithMany(g => g.Vertices)
            .HasForeignKey(v => v.GraphName);

        modelBuilder.Entity<AGEdge>()
            .HasKey(e => new { e.GraphName, e.StartId, e.EndId, e.Label });

        modelBuilder.Entity<AGEdge>()
            .HasOne(e => e.Graph)
            .WithMany(g => g.Edges)
            .HasForeignKey(e => e.GraphName);

        modelBuilder.Entity<AGEdge>()
            .HasOne(e => e.StartVertex)
            .WithMany(v => v.OutgoingEdges)
            .HasForeignKey(e => new { e.GraphName, e.StartId });

        modelBuilder.Entity<AGEdge>()
            .HasOne(e => e.EndVertex)
            .WithMany(v => v.IncomingEdges)
            .HasForeignKey(e => new { e.GraphName, e.EndId });
    }

    private static void ConfigureVectorEntity<T>(ModelBuilder modelBuilder) where T : class
    {
        // The column type is now set using the [Column] attribute in the model
        // We just need to ensure the column name is lowercase
        modelBuilder.Entity<T>()
            .Property("Embedding")
            .HasColumnName("embedding");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Additional configuration if needed
        // Note: The main configuration is done in Program.cs
    }

    public async Task EnsureExtensionAsync()
    {
        try
        {
            Console.WriteLine("Installing PostgreSQL extensions...");

            // Create the pgvector extension if it doesn't exist
            Console.WriteLine("Installing vector extension...");
            await Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS vector");

            // Create the Apache AGE extension if it doesn't exist
            Console.WriteLine("Installing age extension...");
            await Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS age");

            // Load the AGE extension into the current database
            Console.WriteLine("Loading AGE extension...");
            await Database.ExecuteSqlRawAsync("LOAD 'age';");

            // Set the search path to include ag_catalog
            Console.WriteLine("Setting search path for AGE...");
            await Database.ExecuteSqlRawAsync("SET search_path = ag_catalog, '$user', public;");

            // Create the pg_trgm extension for text search
            Console.WriteLine("Installing pg_trgm extension...");
            await Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS pg_trgm");

            // Create the AGE schema if it doesn't exist
            Console.WriteLine("Creating AGE schema if it doesn't exist...");
            await Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS ag_catalog;");

            // Define the default graph name
            var graphName = "pathrag";
            Console.WriteLine($"Ensuring graph {graphName} exists...");

            try {
                // First, try to insert the default graph
                // This will fail if the table doesn't exist, but we'll handle that in the catch block
                await Database.ExecuteSqlRawAsync(@"
                    INSERT INTO ag_catalog.ag_graph (graphid, name, namespace)
                    VALUES (1, @graphName, 'public')
                    ON CONFLICT (name) DO NOTHING;",
                    new NpgsqlParameter("@graphName", graphName));

                Console.WriteLine("Graph tables already exist. Ensuring graph exists.");
            }
            catch (Exception ex) {
                // If we get here, it's likely because the table doesn't exist
                Console.WriteLine($"Creating graph tables: {ex.Message}");

                // Create the graph table if it doesn't exist
                Console.WriteLine("Creating graph table...");
                await Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ag_catalog.ag_graph (
                        graphid SERIAL PRIMARY KEY,
                        name TEXT UNIQUE NOT NULL,
                        namespace TEXT NOT NULL,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );");

                // Insert the default graph
                await Database.ExecuteSqlRawAsync(@"
                    INSERT INTO ag_catalog.ag_graph (graphid, name, namespace)
                    VALUES (1, @graphName, 'public')
                    ON CONFLICT (name) DO NOTHING;",
                    new NpgsqlParameter("@graphName", graphName));

                // Create the vertex table if it doesn't exist
                Console.WriteLine("Creating vertex table...");
                await Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ag_catalog.ag_vertex (
                        graph_name TEXT REFERENCES ag_catalog.ag_graph(name) ON DELETE CASCADE,
                        id TEXT,
                        label TEXT,
                        properties JSONB,
                        PRIMARY KEY (graph_name, id)
                    );");

                // Create the edge table if it doesn't exist
                Console.WriteLine("Creating edge table...");
                await Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ag_catalog.ag_edge (
                        graph_name TEXT REFERENCES ag_catalog.ag_graph(name) ON DELETE CASCADE,
                        start_id TEXT,
                        end_id TEXT,
                        label TEXT,
                        properties JSONB,
                        PRIMARY KEY (graph_name, start_id, end_id, label)
                    );");
            }

            Console.WriteLine("PostgreSQL extensions and tables installed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error installing PostgreSQL extensions: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                Console.WriteLine($"Inner stack trace: {ex.InnerException.StackTrace}");
            }

            throw; // Re-throw the exception to ensure the application fails if extensions can't be installed
        }
    }
}