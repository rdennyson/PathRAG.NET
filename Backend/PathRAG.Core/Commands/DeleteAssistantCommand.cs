using MediatR;
using Microsoft.EntityFrameworkCore;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Core.Commands;

public class DeleteAssistantCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class DeleteAssistantHandler : IRequestHandler<DeleteAssistantCommand, bool>
{
    private readonly PathRagDbContext _dbContext;

    public DeleteAssistantHandler(PathRagDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(DeleteAssistantCommand request, CancellationToken cancellationToken)
    {
        // Find assistant
        var assistant = await _dbContext.Assistants
            .FirstOrDefaultAsync(a => a.Id == request.Id && a.UserId == request.UserId, cancellationToken);

        if (assistant == null)
        {
            return false;
        }

        // Remove assistant
        _dbContext.Assistants.Remove(assistant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
