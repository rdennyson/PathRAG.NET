using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PathRAG.Infrastructure.Models;
[Table("assistantvectorstore", Schema = "public")]
public class AssistantVectorStore
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    public Guid AssistantId { get; set; }
    
    [Required]
    public Guid VectorStoreId { get; set; }
    
    // Navigation properties
    [ForeignKey("AssistantId")]
    public virtual Assistant Assistant { get; set; } = null!;
    
    [ForeignKey("VectorStoreId")]
    public virtual VectorStore VectorStore { get; set; } = null!;
}
