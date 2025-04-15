using MediatR;
using Microsoft.EntityFrameworkCore;
using PathRAG.Core.Models;
using PathRAG.Core.Queries;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Handlers;

public class GetChatSessionsHandler : IRequestHandler<GetChatSessionsQuery, IEnumerable<ChatSession>>
{
    private readonly PathRagDbContext _dbContext;

    public GetChatSessionsHandler(PathRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<ChatSession>> Handle(GetChatSessionsQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.ChatSessions
            .Where(cs => cs.UserId == request.UserId)
            .OrderByDescending(cs => cs.UpdatedAt)
            .ToListAsync(cancellationToken);
    }
}
