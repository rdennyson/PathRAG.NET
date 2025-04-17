using System.Threading;
using System.Threading.Tasks;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Models;

namespace PathRAG.Core.Services.Entity;

public interface IEntityExtractionService
{
    Task<ExtractionResult> ExtractEntitiesAndRelationshipsAsync(
        string text,
        CancellationToken cancellationToken = default
    );
}