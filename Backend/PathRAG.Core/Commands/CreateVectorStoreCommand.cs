using MediatR;
using PathRAG.Core.Models;

namespace PathRAG.Core.Commands;

public class CreateVectorStoreCommand : IRequest<VectorStore>
{
    public string Name { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}
