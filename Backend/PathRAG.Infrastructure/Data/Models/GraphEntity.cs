using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace PathRAG.Infrastructure.Models;
[Table("entity", Schema = "public")]
public class GraphEntity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Type { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "vector")]
    public Vector Embedding { get; set; } = new Vector(Array.Empty<float>());

    public List<string> Keywords { get; set; } = new();

    public float Weight { get; set; }

    public string SourceId { get; set; } = string.Empty;

    [Required]
    public Guid VectorStoreId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("VectorStoreId")]
    public virtual VectorStore? VectorStore { get; set; }
}
