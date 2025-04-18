using MediatR;
using Microsoft.EntityFrameworkCore;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Commands;

public class DeleteChatSessionCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class DeleteChatSessionHandler : IRequestHandler<DeleteChatSessionCommand, bool>
{
    private readonly PathRagDbContext _dbContext;

    public DeleteChatSessionHandler(PathRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(DeleteChatSessionCommand request, CancellationToken cancellationToken)
    {
        // Find chat session
        var chatSession = await _dbContext.ChatSessions
            .FirstOrDefaultAsync(cs => cs.Id == request.Id && cs.UserId == request.UserId, cancellationToken);

        if (chatSession == null)
        {
            return false;
        }

        // Remove chat session
        _dbContext.ChatSessions.Remove(chatSession);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
