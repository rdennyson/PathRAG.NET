using MediatR;
using Microsoft.EntityFrameworkCore;
using PathRAG.Core.Commands;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Handlers;

public class CreateChatSessionHandler : IRequestHandler<CreateChatSessionCommand, ChatSession>
{
    private readonly PathRagDbContext _dbContext;

    public CreateChatSessionHandler(PathRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ChatSession> Handle(CreateChatSessionCommand request, CancellationToken cancellationToken)
    {
        // Verify assistant exists and belongs to user
        var assistant = await _dbContext.Assistants
            .FirstOrDefaultAsync(a => a.Id == request.AssistantId && a.UserId == request.UserId, cancellationToken);

        if (assistant == null)
        {
            throw new KeyNotFoundException($"Assistant with ID {request.AssistantId} not found");
        }

        // Create new chat session
        var chatSession = new ChatSession
        {
            Id = Guid.NewGuid(),
            Title = $"New Chat {DateTime.UtcNow:g}",
            AssistantId = assistant.Id,
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Add to database
        await _dbContext.ChatSessions.AddAsync(chatSession, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return chatSession;
    }
}
