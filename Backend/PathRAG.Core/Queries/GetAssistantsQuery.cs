using MediatR;
using Microsoft.EntityFrameworkCore;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Data;
using PathRAG.Infrastructure.Models;

namespace PathRAG.Core.Queries;

public class GetAssistantsQuery : IRequest<IEnumerable<Assistant>>
{
    public string UserId { get; set; } = string.Empty;
}

public class GetAssistantsHandler : IRequestHandler<GetAssistantsQuery, IEnumerable<Assistant>>
{
    private readonly PathRagDbContext _dbContext;

    public GetAssistantsHandler(PathRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<Assistant>> Handle(GetAssistantsQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Assistants
            .Include(a => a.AssistantVectorStores)
            .Where(a => a.UserId == request.UserId)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync(cancellationToken);
    }
}
