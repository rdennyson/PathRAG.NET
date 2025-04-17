using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace PathRAG.Infrastructure.Models;
[Table("relationship", Schema = "public")]
public class Relationship
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string SourceEntityId { get; set; } = string.Empty;

    [Required]
    public string TargetEntityId { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Type { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "vector")]
    public Vector Embedding { get; set; } = new Vector(Array.Empty<float>());

    public float Weight { get; set; }

    public List<string> Keywords { get; set; } = new();

    public string SourceId { get; set; } = string.Empty;

    [Required]
    public Guid VectorStoreId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("VectorStoreId")]
    public virtual VectorStore? VectorStore { get; set; }
}
