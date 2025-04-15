using Microsoft.Extensions.Configuration;
using Moq;
using Npgsql;
using PathRAG.Core.Models;
using PathRAG.Core.Services.Graph;

namespace PathRAG.Tests;

public class PostgresAGEGraphStorageServiceTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<NpgsqlConnection> _mockConnection;
    private readonly PostgresAGEGraphStorageService _graphStorageService;

    public PostgresAGEGraphStorageServiceTests()
    {
        // Setup mocks
        _mockConfiguration = new Mock<IConfiguration>();
        _mockConnection = new Mock<NpgsqlConnection>();

        // Setup configuration
        _mockConfiguration.Setup(x => x.GetConnectionString("DefaultConnection"))
            .Returns("Host=localhost;Database=pathrag;Username=postgres;Password=your_password");

        // Create service with a factory that returns our mock connection
        _graphStorageService = new PostgresAGEGraphStorageService(
            _mockConfiguration.Object,
            () => _mockConnection.Object);
    }

    [Fact]
    public async Task InitializeAsync_CreatesGraph()
    {
        // Arrange
        var mockCommand = new Mock<NpgsqlCommand>();
        _mockConnection.Setup(x => x.CreateCommand()).Returns(mockCommand.Object);

        // Act
        await _graphStorageService.InitializeAsync();

        // Assert
        _mockConnection.Verify(x => x.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockCommand.Verify(x => x.ExecuteNonQueryAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task AddEntityAsync_AddsEntityToGraph()
    {
        // Arrange
        var entity = new GraphEntity
        {
            Id = Guid.NewGuid(),
            Name = "TestEntity",
            Type = "TestType",
            Description = "Test description",
            Keywords = new List<string> { "keyword1", "keyword2" }
        };

        var mockCommand = new Mock<NpgsqlCommand>();
        _mockConnection.Setup(x => x.CreateCommand()).Returns(mockCommand.Object);

        // Act
        await _graphStorageService.AddEntityAsync(entity);

        // Assert
        _mockConnection.Verify(x => x.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockCommand.Verify(x => x.ExecuteNonQueryAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddRelationshipAsync_AddsRelationshipToGraph()
    {
        // Arrange
        var relationship = new Relationship
        {
            Id = Guid.NewGuid(),
            SourceEntityId = "source-id",
            TargetEntityId = "target-id",
            Type = "RELATED_TO",
            Description = "Test relationship"
        };

        var mockCommand = new Mock<NpgsqlCommand>();
        _mockConnection.Setup(x => x.CreateCommand()).Returns(mockCommand.Object);

        // Act
        await _graphStorageService.AddRelationshipAsync(relationship);

        // Assert
        _mockConnection.Verify(x => x.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockCommand.Verify(x => x.ExecuteNonQueryAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRelatedNodesAsync_ReturnsRelatedNodes()
    {
        // Arrange
        var nodeId = "test-node-id";
        var mockCommand = new Mock<NpgsqlCommand>();
        var mockReader = new Mock<NpgsqlDataReader>();

        _mockConnection.Setup(x => x.CreateCommand()).Returns(mockCommand.Object);
        mockCommand.Setup(x => x.ExecuteReaderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockReader.Object);

        // Setup reader to return some data
        var callCount = 0;
        mockReader.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => callCount++ < 2); // Return true twice, then false

        mockReader.Setup(x => x.GetString(0)).Returns("entity-id");
        mockReader.Setup(x => x.GetString(1)).Returns("Entity");
        mockReader.Setup(x => x.GetString(2)).Returns("TestEntity");
        mockReader.Setup(x => x.GetString(3)).Returns("Test description");

        // Act
        var result = await _graphStorageService.GetRelatedNodesAsync(nodeId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }
}
