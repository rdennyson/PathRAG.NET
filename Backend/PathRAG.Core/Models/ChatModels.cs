using Microsoft.AspNetCore.Http;

namespace PathRAG.Core.Models;

public class ChatSessionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid AssistantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ChatSessionDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid AssistantId { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = new List<ChatMessageDto>();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<string>? Attachments { get; set; }
}

public class CreateChatSessionRequest
{
    public Guid AssistantId { get; set; }
}

public class AddChatMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public List<IFormFile>? Attachments { get; set; }
}
