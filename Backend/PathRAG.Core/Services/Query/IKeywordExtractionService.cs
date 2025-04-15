using System.Threading;
using System.Threading.Tasks;

namespace PathRAG.Core.Services.Query;

public record KeywordExtractionResult(
    IReadOnlyList<string> HighLevelKeywords,
    IReadOnlyList<string> LowLevelKeywords
);

public interface IKeywordExtractionService
{
    Task<KeywordExtractionResult> ExtractKeywordsAsync(
        string text,
        CancellationToken cancellationToken = default
    );
}