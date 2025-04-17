using MediatR;
using Microsoft.EntityFrameworkCore;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Data;
using PathRAG.Infrastructure.Models;

namespace PathRAG.Core.Queries;

public class GetVectorStoresQuery : IRequest<IEnumerable<VectorStore>>
{
    public string UserId { get; set; } = string.Empty;
}

public class GetVectorStoresHandler : IRequestHandler<GetVectorStoresQuery, IEnumerable<VectorStore>>
{
    private readonly PathRagDbContext _dbContext;

    public GetVectorStoresHandler(PathRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<VectorStore>> Handle(GetVectorStoresQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.VectorStores
            .Include(vs => vs.TextChunks)
            .Where(vs => vs.UserId == request.UserId)
            .OrderByDescending(vs => vs.UpdatedAt)
            .ToListAsync(cancellationToken);
    }
}
