using MediatR;
using Microsoft.EntityFrameworkCore;
using PathRAG.Core.Models;
using PathRAG.Core.Queries;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Handlers;

public class GetAssistantByIdHandler : IRequestHandler<GetAssistantByIdQuery, Assistant?>
{
    private readonly PathRagDbContext _dbContext;

    public GetAssistantByIdHandler(PathRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Assistant?> Handle(GetAssistantByIdQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Assistants
            .Include(a => a.AssistantVectorStores)
            .FirstOrDefaultAsync(a => a.Id == request.Id && a.UserId == request.UserId, cancellationToken);
    }
}
