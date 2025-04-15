using PathRAG.Core.Models;
using System.Text;

namespace PathRAG.Core.Services.Query;

public class ContextBuilderService : IContextBuilderService
{
    public async Task<string> BuildContextAsync(
        IReadOnlyList<TextChunk> chunks,
        IReadOnlyList<GraphEntity> entities,
        IReadOnlyList<Relationship> relationships,
        CancellationToken cancellationToken = default)
    {
        var context = new StringBuilder();

        // Add text chunks
        if (chunks.Any())
        {
            context.AppendLine("Relevant Text Passages:");
            foreach (var chunk in chunks)
            {
                context.AppendLine($"- {chunk.Content}");
            }
            context.AppendLine();
        }

        // Add entities
        if (entities.Any())
        {
            context.AppendLine("Related Entities:");
            foreach (var entity in entities)
            {
                context.AppendLine($"- {entity.Name} ({entity.Type}): {entity.Description}");
            }
            context.AppendLine();
        }

        // Add relationships
        if (relationships.Any())
        {
            // Create a dictionary to look up entity names by ID
            var entityNameMap = entities.ToDictionary(
                e => e.Id.ToString(),
                e => e.Name);

            context.AppendLine("Entity Relationships:");
            foreach (var rel in relationships)
            {
                // Try to get source and target entity names from the map
                string sourceEntityName = GetEntityName(entityNameMap, rel.SourceEntityId);
                string targetEntityName = GetEntityName(entityNameMap, rel.TargetEntityId);

                context.AppendLine($"- {sourceEntityName} {rel.Type} {targetEntityName}: {rel.Description}");
            }
        }

        return context.ToString().Trim();
    }

    private string GetEntityName(Dictionary<string, string> entityNameMap, string entityId)
    {
        // Try to get the entity name from the map, or use the ID if not found
        return entityNameMap.TryGetValue(entityId, out var name) ? name : entityId;
    }
}