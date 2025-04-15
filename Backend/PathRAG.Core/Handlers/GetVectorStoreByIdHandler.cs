using MediatR;
using Microsoft.EntityFrameworkCore;
using PathRAG.Core.Models;
using PathRAG.Core.Queries;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Handlers;

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
