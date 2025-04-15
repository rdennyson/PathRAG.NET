using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PathRAG.Core.Models;
using PathRAG.Core.Services.Graph;
using SharpToken;

namespace PathRAG.Core.Services.Embedding;

public class EntityEmbeddingService : IEntityEmbeddingService
{
    private readonly OpenAIClient _openAIClient;
    private readonly IOptions<PathRagOptions> _options;
    private readonly IGraphStorageService _graphStorage;
    private readonly ILogger<EntityEmbeddingService> _logger;
    private readonly GptEncoding _encoding;

    public EntityEmbeddingService(
        IOptions<PathRagOptions> options,
        OpenAIClient openAIClient,
        IGraphStorageService graphStorage,
        ILogger<EntityEmbeddingService> logger)
    {
        _options = options;
        _openAIClient = openAIClient;
        _graphStorage = graphStorage;
        _encoding = GptEncoding.GetEncodingForModel(GetTikTokenModelName(_options.Value.EmbeddingModel));
        _logger = logger;
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var response = await _openAIClient.GetEmbeddingsAsync(
            new EmbeddingsOptions(_options.Value.EmbeddingDeployment, new List<string> { text }),
            cancellationToken
        );

        return response.Value.Data[0].Embedding.ToArray();
    }

    public async Task<List<float[]>> GetEmbeddingsAsync(
        List<string> texts,
        CancellationToken cancellationToken = default)
    {
        var embeddings = new List<float[]>();

        // Process in batches to avoid rate limits
        foreach (var batch in texts.Chunk(20))
        {
            var tasks = batch.Select(text => GetEmbeddingAsync(text, cancellationToken));
            var batchResults = await Task.WhenAll(tasks);
            embeddings.AddRange(batchResults);
        }

        return embeddings;
    }

    public Task<int> GetTokenCount(string text)
    {
        var tokens = _encoding.Encode(text);
        return Task.FromResult(tokens.Count);
    }
    public async Task<Dictionary<string, float[]>> GetNodeEmbeddingsAsync(
        IEnumerable<GraphEntity> entities,
        string algorithm = "node2vec",
        CancellationToken cancellationToken = default)
    {
        var embeddings = new Dictionary<string, float[]>();

        if (algorithm == "node2vec")
        {
            // Get graph embeddings using node2vec algorithm
            var graphEmbeddings = await _graphStorage.EmbedNodesAsync(algorithm, cancellationToken);

            // Map embeddings to entities
            int i = 0;
            foreach (var entity in entities)
            {
                embeddings[entity.Id.ToString()] = graphEmbeddings;
                i++;
            }
        }
        else
        {
            // Fallback to text embeddings
            foreach (var entity in entities)
            {
                var embedding = await GetEmbeddingAsync(
                    $"{entity.Name} {entity.Description}",
                    cancellationToken
                );
                embeddings[entity.Id.ToString()] = embedding;
            }
        }

        return embeddings;
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