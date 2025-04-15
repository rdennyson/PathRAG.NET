using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PathRAG.Core.Commands;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Handlers;

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
        
        // Update chat session
        chatSession.UpdatedAt = DateTime.UtcNow;
        _dbContext.ChatSessions.Update(chatSession);

        // Process attachments if any
        if (request.Attachments != null && request.Attachments.Count > 0)
        {
            foreach (var file in request.Attachments)
            {
                // Create a unique filename
                var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                var storagePath = Path.Combine("uploads", fileName);
                
                // Ensure directory exists
                var directory = Path.GetDirectoryName(storagePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }
                
                // Save file
                using (var stream = new FileStream(storagePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }
                
                // Create attachment record
                var attachment = new MessageAttachment
                {
                    Id = Guid.NewGuid(),
                    ChatMessageId = chatMessage.Id,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    Size = file.Length,
                    StoragePath = storagePath,
                    UploadedAt = DateTime.UtcNow
                };
                
                await _dbContext.MessageAttachments.AddAsync(attachment, cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return chatMessage;
    }
}
