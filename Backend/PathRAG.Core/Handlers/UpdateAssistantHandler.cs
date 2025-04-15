using MediatR;
using Microsoft.EntityFrameworkCore;
using PathRAG.Core.Commands;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Handlers;

public class UpdateAssistantHandler : IRequestHandler<UpdateAssistantCommand, Assistant>
{
    private readonly PathRagDbContext _dbContext;

    public UpdateAssistantHandler(PathRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Assistant> Handle(UpdateAssistantCommand request, CancellationToken cancellationToken)
    {
        // Find assistant
        var assistant = await _dbContext.Assistants
            .Include(a => a.AssistantVectorStores)
            .FirstOrDefaultAsync(a => a.Id == request.Id && a.UserId == request.UserId, cancellationToken);

        if (assistant == null)
        {
            throw new KeyNotFoundException($"Assistant with ID {request.Id} not found");
        }

        // Update properties
        if (request.Name != null)
        {
            assistant.Name = request.Name;
        }

        if (request.Message != null)
        {
            assistant.Message = request.Message;
        }

        if (request.Temperature.HasValue)
        {
            assistant.Temperature = request.Temperature.Value;
        }

        assistant.UpdatedAt = DateTime.UtcNow;

        // Update vector store associations if provided
        if (request.VectorStoreIds != null)
        {
            // Remove existing associations
            _dbContext.AssistantVectorStores.RemoveRange(assistant.AssistantVectorStores);

            // Add new associations
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
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return assistant;
    }
}
