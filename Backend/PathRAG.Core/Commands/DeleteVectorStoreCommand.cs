using MediatR;
using Microsoft.EntityFrameworkCore;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Commands;

public class DeleteVectorStoreCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class DeleteVectorStoreHandler : IRequestHandler<DeleteVectorStoreCommand, bool>
{
    private readonly PathRagDbContext _dbContext;

    public DeleteVectorStoreHandler(PathRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(DeleteVectorStoreCommand request, CancellationToken cancellationToken)
    {
        // Find vector store
        var vectorStore = await _dbContext.VectorStores
            .FirstOrDefaultAsync(vs => vs.Id == request.Id && vs.UserId == request.UserId, cancellationToken);

        if (vectorStore == null)
        {
            return false;
        }

        // Remove vector store
        _dbContext.VectorStores.Remove(vectorStore);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
