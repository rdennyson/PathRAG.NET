using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Models;
using SharpToken;
using System.Text;
using System.Text.RegularExpressions;

namespace PathRAG.Core.Services;

public interface ITextChunkService
{
    List<TextChunk> ChunkDocument(string content);
}

public class TextChunkService : ITextChunkService
{
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;
    private readonly GptEncoding _encoding;
    private readonly string _modelName;

    public TextChunkService(IOptions<PathRagOptions> options)
    {
        var config = options.Value;
        _chunkSize = config.ChunkSize;
        _chunkOverlap = config.ChunkOverlap;
        _modelName = config.CompletionModel;

        // Initialize the tokenizer based on the model
        _encoding = GptEncoding.GetEncodingForModel(GetTikTokenModelName(_modelName));
    }

    public List<TextChunk> ChunkDocument(string content)
    {
        if (string.IsNullOrEmpty(content))
            return new List<TextChunk>();

        // Encode the content into tokens
        var tokens = _encoding.Encode(content);
        var results = new List<TextChunk>();

        // Calculate chunk boundaries
        for (int index = 0, start = 0; start < tokens.Count; index++, start += (_chunkSize - _chunkOverlap))
        {
            // Get the token slice for this chunk
            var end = Math.Min(start + _chunkSize, tokens.Count);
            var chunkTokens = tokens.GetRange(start, end - start);

            // Decode the tokens back to text
            var chunkContent = _encoding.Decode(chunkTokens);

            // Sanitize the content to remove null bytes and other problematic characters
            var sanitizedContent = SanitizeText(chunkContent.Trim());

            // Create the chunk
            results.Add(new TextChunk
            {
                Id = Guid.NewGuid(),
                Content = sanitizedContent,
                TokenCount = chunkTokens.Count,
                ChunkOrderIndex = index,
                FullDocumentId = Guid.NewGuid().ToString(), // This would be set by the caller
                CreatedAt = DateTime.UtcNow
            });

            // If we've reached the end of the document, break
            if (end == tokens.Count) break;
        }

        return results;
    }

    private string SanitizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // Remove null bytes (0x00) which cause issues with PostgreSQL UTF-8 encoding
        text = text.Replace("\0", "");

        // Remove other control characters except for newlines and tabs
        text = Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");

        // Replace multiple whitespace characters with a single space
        text = Regex.Replace(text, @"\s+", " ");

        // Trim leading/trailing whitespace
        return text.Trim();
    }

    private string GetTikTokenModelName(string modelName)
    {
        // Map Azure OpenAI model names to tiktoken model names
        return modelName.ToLowerInvariant() switch
        {
            var name when name.Contains("gpt-4") => "gpt-4",
            var name when name.Contains("gpt-35-turbo") => "gpt-3.5-turbo",
            var name when name.Contains("text-embedding") => "text-embedding-ada-002",
            _ => "gpt-4" // Default to gpt-4
        };
    }
}