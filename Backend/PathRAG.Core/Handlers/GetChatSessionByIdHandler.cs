using MediatR;
using Microsoft.EntityFrameworkCore;
using PathRAG.Core.Models;
using PathRAG.Core.Queries;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Handlers;

public class GetChatSessionByIdHandler : IRequestHandler<GetChatSessionByIdQuery, ChatSession?>
{
    private readonly PathRagDbContext _dbContext;

    public GetChatSessionByIdHandler(PathRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ChatSession?> Handle(GetChatSessionByIdQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.ChatSessions
            .Include(cs => cs.Messages)
            .ThenInclude(m => m.Attachments)
            .FirstOrDefaultAsync(cs => cs.Id == request.Id && cs.UserId == request.UserId, cancellationToken);
    }
}
