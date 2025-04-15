using MediatR;
using PathRAG.Core.Commands;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Handlers;

public class CreateVectorStoreHandler : IRequestHandler<CreateVectorStoreCommand, VectorStore>
{
    private readonly PathRagDbContext _dbContext;

    public CreateVectorStoreHandler(PathRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<VectorStore> Handle(CreateVectorStoreCommand request, CancellationToken cancellationToken)
    {
        // Create new vector store
        var vectorStore = new VectorStore
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Add to database
        await _dbContext.VectorStores.AddAsync(vectorStore, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return vectorStore;
    }
}
