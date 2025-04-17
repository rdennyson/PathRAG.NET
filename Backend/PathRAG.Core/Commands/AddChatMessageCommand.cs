using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Data;
using PathRAG.Infrastructure.Models;

namespace PathRAG.Core.Commands;

public class AddChatMessageCommand : IRequest<ChatMessage>
{
    public Guid ChatSessionId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public List<IFormFile>? Attachments { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class AddChatMessageHandler : IRequestHandler<AddChatMessageCommand, ChatMessage>
{
    private readonly PathRagDbContext _dbContext;
    private readonly ILogger<AddChatMessageHandler> _logger;

    public AddChatMessageHandler(
        PathRagDbContext dbContext,
        ILogger<AddChatMessageHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ChatMessage> Handle(AddChatMessageCommand request, CancellationToken cancellationToken)
    {
        // Verify chat session exists and belongs to user
        var chatSession = await _dbContext.ChatSessions
            .FirstOrDefaultAsync(cs => cs.Id == request.ChatSessionId && cs.UserId == request.UserId, cancellationToken);

        if (chatSession == null)
        {
            throw new KeyNotFoundException($"Chat session with ID {request.ChatSessionId} not found");
        }

        // Create new chat message
        var chatMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            Content = request.Content,
            Role = request.Role,
            ChatSessionId = chatSession.Id,
            Timestamp = DateTime.UtcNow
        };

        // Add to database
        await _dbContext.ChatMessages.AddAsync(chatMessage, cancellationToken);

        // Process attachments if any
        if (request.Attachments != null && request.Attachments.Count > 0)
        {
            foreach (var attachment in request.Attachments)
            {
                // Read file content
                using var memoryStream = new MemoryStream();
                await attachment.CopyToAsync(memoryStream, cancellationToken);
                var fileContent = memoryStream.ToArray();

                // Create attachment record
                var messageAttachment = new MessageAttachment
                {
                    Id = Guid.NewGuid(),
                    ChatMessageId = chatMessage.Id,
                    FileName = attachment.FileName,
                    ContentType = attachment.ContentType,
                    Size = attachment.Length,
                    //Content = fileContent,
                    //CreatedAt = DateTime.UtcNow
                };

                await _dbContext.MessageAttachments.AddAsync(messageAttachment, cancellationToken);
            }
        }

        // Update chat session's UpdatedAt timestamp
        chatSession.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Load attachments for the response
        await _dbContext.Entry(chatMessage)
            .Collection(m => m.Attachments)
            .LoadAsync(cancellationToken);

        return chatMessage;
    }
}
