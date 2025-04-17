using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PathRAG.Infrastructure.Models;
[Table("assistant", Schema = "public")]
public class Assistant
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public string Message { get; set; } = string.Empty;
    
    [Required]
    [Range(0, 1)]
    public float Temperature { get; set; } = 0.7f;
    
    public string UserId { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<AssistantVectorStore> AssistantVectorStores { get; set; } = new List<AssistantVectorStore>();
    
    public virtual ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
}
