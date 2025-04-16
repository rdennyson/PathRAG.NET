using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PathRAG.Core;
using PathRAG.Core.Services.Cache;
using PathRAG.Core.Services.Embedding;

namespace PathRAG.Tests;

public class EmbeddingServiceTests
{
    private readonly Mock<OpenAIClient> _mockOpenAIClient;
    private readonly Mock<IOptions<PathRagOptions>> _mockOptions;
    private readonly Mock<IEmbeddingCacheService> _mockCacheService;
    private readonly Mock<ILogger<EmbeddingService>> _mockLogger;
    private readonly EmbeddingService _embeddingService;

    public EmbeddingServiceTests()
    {
        // Setup mocks
        _mockOpenAIClient = new Mock<OpenAIClient>();
        _mockOptions = new Mock<IOptions<PathRagOptions>>();
        _mockCacheService = new Mock<IEmbeddingCacheService>();
        _mockLogger = new Mock<ILogger<EmbeddingService>>();

        // Setup options
        _mockOptions.Setup(x => x.Value).Returns(new PathRagOptions
        {
            EmbeddingDeployment = "text-embedding-3-large",
            EnableEmbeddingCache = true,
            EnableSimilarityCache = true,
            SimilarityThreshold = 0.95f
        });

        // Create service
        _embeddingService = new EmbeddingService(
            _mockOpenAIClient.Object,
            _mockOptions.Object,
            _mockCacheService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetEmbeddingAsync_CachedText_ReturnsCachedEmbedding()
    {
        // Arrange
        var text = "This is a test text";
        var cachedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };

        _mockCacheService.Setup(x => x.TryGetEmbeddingAsync(text, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, cachedEmbedding));

        // Act
        var result = await _embeddingService.GetEmbeddingAsync(text);

        // Assert
        Assert.Equal(cachedEmbedding, result);
        _mockOpenAIClient.Verify(x => x.GetEmbeddingsAsync(
            It.IsAny<EmbeddingsOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetEmbeddingAsync_UncachedText_CallsOpenAIAndCachesResult()
    {
        // Arrange
        var text = "This is a test text";
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        // Setup cache miss
        _mockCacheService.Setup(x => x.TryGetEmbeddingAsync(text, It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, null));

        // Setup OpenAI response
        var mockResponse = new Mock<Response<Embeddings>>();
        var mockEmbeddings = new Mock<Embeddings>();
        var mockEmbeddingItem = new EmbeddingItem(0, embedding);

        mockEmbeddings.Setup(x => x.Data).Returns(new[] { mockEmbeddingItem });
        mockResponse.Setup(x => x.Value).Returns(mockEmbeddings.Object);

        _mockOpenAIClient.Setup(x => x.GetEmbeddingsAsync(
            It.IsAny<EmbeddingsOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        // Act
        var result = await _embeddingService.GetEmbeddingAsync(text);

        // Assert
        Assert.Equal(embedding, result);
        _mockCacheService.Verify(x => x.CacheEmbeddingAsync(
            text,
            embedding,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetEmbeddingsAsync_BatchTexts_ProcessesAllTexts()
    {
        // Arrange
        var texts = new List<string> { "Text 1", "Text 2", "Text 3" };

        // Setup cache hits and misses
        _mockCacheService.Setup(x => x.TryGetEmbeddingAsync("Text 1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new float[] { 0.1f, 0.2f, 0.3f }));
        _mockCacheService.Setup(x => x.TryGetEmbeddingAsync("Text 2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, null));
        _mockCacheService.Setup(x => x.TryGetEmbeddingAsync("Text 3", It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, null));

        // Setup OpenAI response for uncached texts
        var mockResponse = new Mock<Response<Embeddings>>();
        var mockEmbeddings = new Mock<Embeddings>();
        var embeddingItems = new List<EmbeddingItem>
        {
            new EmbeddingItem(0, new float[] { 0.4f, 0.5f, 0.6f }),
            new EmbeddingItem(1, new float[] { 0.7f, 0.8f, 0.9f })
        };

        mockEmbeddings.Setup(x => x.Data).Returns(embeddingItems);
        mockResponse.Setup(x => x.Value).Returns(mockEmbeddings.Object);

        _mockOpenAIClient.Setup(x => x.GetEmbeddingsAsync(
            It.IsAny<EmbeddingsOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        // Act
        var result = await _embeddingService.GetEmbeddingsAsync(texts);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(new float[] { 0.1f, 0.2f, 0.3f }, result[0]);
        Assert.Equal(new float[] { 0.4f, 0.5f, 0.6f }, result[1]);
        Assert.Equal(new float[] { 0.7f, 0.8f, 0.9f }, result[2]);
    }
}
