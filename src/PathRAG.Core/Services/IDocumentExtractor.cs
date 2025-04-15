using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PathRAG.Core.Services;

public interface IDocumentExtractor
{
    /// <summary>
    /// Extracts text content from a document stream.
    /// </summary>
    /// <param name="stream">The document stream</param>
    /// <param name="fileName">Original filename with extension</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extracted text content</returns>
    Task<string> ExtractTextAsync(
        Stream stream, 
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the given file type is supported.
    /// </summary>
    /// <param name="fileName">Filename with extension</param>
    /// <returns>True if supported, false otherwise</returns>
    bool IsSupported(string fileName);
}