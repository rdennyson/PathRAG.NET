using MediatR;

namespace PathRAG.Core.Commands;

public class DeleteAssistantCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
}
