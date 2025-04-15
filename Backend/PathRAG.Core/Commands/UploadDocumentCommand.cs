using MediatR;
using Microsoft.AspNetCore.Http;
using PathRAG.Core.Models;

namespace PathRAG.Core.Commands;

public class UploadDocumentCommand : IRequest<TextChunk[]>
{
    public Guid VectorStoreId { get; set; }
    public IFormFile File { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
}
