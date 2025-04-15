using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PathRAG.Core.Models;

public class ChatSession
{
    [Key]
    public Guid Id { get; set; }
    
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public Guid AssistantId { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    [ForeignKey("AssistantId")]
    public virtual Assistant Assistant { get; set; } = null!;
    
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
