namespace PathRAG.Core;

public class PathRagOptions
{
    // Azure OpenAI Settings
    public string Endpoint { get; set; } = "https://your-resource.openai.azure.com/";
    public string ApiKey { get; set; } = "your-api-key";
    public string ApiVersion { get; set; } = "2023-12-01-preview";
    public string DeploymentName { get; set; } = "gpt-4";
    public string EmbeddingDeployment { get; set; } = "text-embedding-3-large";

    // Model Settings
    public string CompletionModel { get; set; } = "gpt-4";
    public string EmbeddingModel { get; set; } = "text-embedding-3-large";
    public string KeywordExtractionModel { get; set; } = "gpt-4";

    // Document Processing Settings
    public string WorkingDirectory { get; set; } = "./data";
    public int MaxDocumentLength { get; set; } = 1000000;
    public int EntityExtractMaxGleaning { get; set; } = 1;
    public int EntitySummaryMaxTokens { get; set; } = 500;

    // Chunking Settings
    public int ChunkSize { get; set; } = 1200;
    public int ChunkOverlap { get; set; } = 100;

    // Query Settings
    public int MaxTokens { get; set; } = 4096; // Reduced from 32768 to avoid context length issues
    public int MaxInputTokens { get; set; } = 100000; // Maximum input tokens for entity extraction
    public float Temperature { get; set; } = 0.7f;
    public int TopK { get; set; } = 40;

    // Cache Settings
    public bool EnableEmbeddingCache { get; set; } = true;
    public bool EnableLLMResponseCache { get; set; } = true;
    public int CacheExpirationMinutes { get; set; } = 60;

    // Similarity-based Caching Settings
    public bool EnableSimilarityCache { get; set; } = true;
    public float SimilarityThreshold { get; set; } = 0.95f;
    public bool UseLLMCheckForSimilarity { get; set; } = false;
}