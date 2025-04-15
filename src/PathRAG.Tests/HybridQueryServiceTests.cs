using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using PathRAG.Core;
using PathRAG.Core.Models;
using PathRAG.Core.Services.Entity;
using PathRAG.Core.Services.Graph;
using PathRAG.Core.Services.Query;
using PathRAG.Infrastructure.Data;

namespace PathRAG.Tests;

public class HybridQueryServiceTests
{
    private readonly Mock<PathRagDbContext> _mockDbContext;
    private readonly Mock<IGraphStorageService> _mockGraphStorage;
    private readonly Mock<IEntityExtractionService> _mockEntityExtractor;
    private readonly Mock<IOptions<PathRagOptions>> _mockOptions;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly HybridQueryService _hybridQueryService;
    private readonly Mock<DbSet<TextChunk>> _mockTextChunks;

    public HybridQueryServiceTests()
    {
        // Setup mocks
        _mockDbContext = new Mock<PathRagDbContext>(new DbContextOptions<PathRagDbContext>());
        _mockGraphStorage = new Mock<IGraphStorageService>();
        _mockEntityExtractor = new Mock<IEntityExtractionService>();
        _mockOptions = new Mock<IOptions<PathRagOptions>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockTextChunks = new Mock<DbSet<TextChunk>>();

        // Setup options
        _mockOptions.Setup(x => x.Value).Returns(new PathRagOptions
        {
            TopK = 5
        });

        // Setup configuration
        _mockConfiguration.Setup(x => x.GetConnectionString("DefaultConnection"))
            .Returns("Host=localhost;Database=pathrag;Username=postgres;Password=your_password");

        // Setup DbContext
        _mockDbContext.Setup(x => x.TextChunks).Returns(_mockTextChunks.Object);

        // Create service
        _hybridQueryService = new HybridQueryService(
            _mockDbContext.Object,
            _mockGraphStorage.Object,
            _mockEntityExtractor.Object,
            _mockOptions.Object,
            _mockConfiguration.Object);
    }

    [Fact]
    public async Task SearchAsync_WithValidQuery_ReturnsSearchResult()
    {
        // Arrange
        var queryEmbedding = new float[1536]; // Empty embedding for testing
        var highLevelKeywords = new List<string> { "keyword1", "keyword2" };
        var lowLevelKeywords = new List<string> { "detail1", "detail2" };

        var textChunks = new List<TextChunk>
        {
            new TextChunk
            {
                Id = Guid.NewGuid(),
                Content = "This is a test chunk containing detail1",
                Embedding = new float[1536],
                CreatedAt = DateTime.UtcNow
            }
        };

        var entities = new List<GraphEntity>
        {
            new GraphEntity
            {
                Id = Guid.NewGuid(),
                Name = "Entity1",
                Type = "Type1",
                Description = "Description1"
            }
        };

        // Setup mock responses
        _mockGraphStorage.Setup(x => x.GetRelatedNodesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities);

        // Mock the LINQ query for keyword search
        var mockQueryable = textChunks.AsQueryable();
        _mockTextChunks.As<IQueryable<TextChunk>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        _mockTextChunks.As<IQueryable<TextChunk>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        _mockTextChunks.As<IQueryable<TextChunk>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        _mockTextChunks.As<IQueryable<TextChunk>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

        // Act
        var result = await _hybridQueryService.SearchAsync(
            queryEmbedding,
            highLevelKeywords,
            lowLevelKeywords,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Entities);
        // Note: We can't easily test the chunks due to the complexity of mocking EF Core queries
    }
}
