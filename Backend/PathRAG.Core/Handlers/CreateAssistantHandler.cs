using MediatR;
using Microsoft.EntityFrameworkCore;
using PathRAG.Core.Commands;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Handlers;

public class CreateAssistantHandler : IRequestHandler<CreateAssistantCommand, Assistant>
{
    private readonly PathRagDbContext _dbContext;

    public CreateAssistantHandler(PathRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Assistant> Handle(CreateAssistantCommand request, CancellationToken cancellationToken)
    {
        // Create new assistant
        var assistant = new Assistant
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Message = request.Message,
            Temperature = request.Temperature,
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Add to database
        await _dbContext.Assistants.AddAsync(assistant, cancellationToken);

        // Add vector store associations
        foreach (var vectorStoreId in request.VectorStoreIds)
        {
            var vectorStore = await _dbContext.VectorStores.FindAsync(new object[] { vectorStoreId }, cancellationToken);
            if (vectorStore != null)
            {
                var association = new AssistantVectorStore
                {
                    AssistantId = assistant.Id,
                    VectorStoreId = vectorStoreId
                };
                await _dbContext.AssistantVectorStores.AddAsync(association, cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return assistant;
    }
}
