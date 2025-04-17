using MediatR;
using Microsoft.EntityFrameworkCore;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Data;
using PathRAG.Infrastructure.Models;

namespace PathRAG.Core.Queries;

public class GetVectorStoreByIdQuery : IRequest<VectorStore?>
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class GetVectorStoreByIdHandler : IRequestHandler<GetVectorStoreByIdQuery, VectorStore?>
{
    private readonly PathRagDbContext _dbContext;

    public GetVectorStoreByIdHandler(PathRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<VectorStore?> Handle(GetVectorStoreByIdQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.VectorStores
            .Include(vs => vs.TextChunks)
            .FirstOrDefaultAsync(vs => vs.Id == request.Id && vs.UserId == request.UserId, cancellationToken);
    }
}
