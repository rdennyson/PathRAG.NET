using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PathRAG.Infrastructure.Models;
[Table("messageattachment", Schema = "public")]
public class MessageAttachment
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    public Guid ChatMessageId { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;
    
    [Required]
    public long Size { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string StoragePath { get; set; } = string.Empty;
    
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    [ForeignKey("ChatMessageId")]
    public virtual ChatMessage ChatMessage { get; set; } = null!;
}
