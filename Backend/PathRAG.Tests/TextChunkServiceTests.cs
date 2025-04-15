using Microsoft.Extensions.Options;
using Moq;
using PathRAG.Core;
using PathRAG.Core.Services;

namespace PathRAG.Tests;

public class TextChunkServiceTests
{
    private readonly TextChunkService _textChunkService;
    private readonly Mock<IOptions<PathRagOptions>> _mockOptions;

    public TextChunkServiceTests()
    {
        // Setup mock options
        _mockOptions = new Mock<IOptions<PathRagOptions>>();
        _mockOptions.Setup(x => x.Value).Returns(new PathRagOptions
        {
            ChunkSize = 10,
            ChunkOverlap = 2,
            CompletionModel = "gpt-4"
        });

        _textChunkService = new TextChunkService(_mockOptions.Object);
    }

    [Fact]
    public void ChunkDocument_EmptyContent_ReturnsEmptyList()
    {
        // Arrange
        var content = string.Empty;

        // Act
        var result = _textChunkService.ChunkDocument(content);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ChunkDocument_ShortContent_ReturnsSingleChunk()
    {
        // Arrange
        var content = "This is a short text.";

        // Act
        var result = _textChunkService.ChunkDocument(content);

        // Assert
        Assert.Single(result);
        Assert.Equal(content.Trim(), result[0].Content);
    }

    [Fact]
    public void ChunkDocument_LongContent_ReturnsMultipleChunks()
    {
        // Arrange
        var content = "This is a longer text that should be split into multiple chunks. " +
                     "It contains enough tokens to create at least two chunks with the configured settings.";

        // Act
        var result = _textChunkService.ChunkDocument(content);

        // Assert
        Assert.True(result.Count > 1);
        Assert.Equal(0, result[0].ChunkOrderIndex);
        Assert.Equal(1, result[1].ChunkOrderIndex);
    }

    [Fact]
    public void ChunkDocument_WithOverlap_ChunksHaveOverlappingContent()
    {
        // Arrange - Create a text with distinct words that we can easily check for overlap
        var content = "Word1 Word2 Word3 Word4 Word5 Word6 Word7 Word8 Word9 Word10 Word11 Word12 Word13 Word14 Word15";

        // Act
        var result = _textChunkService.ChunkDocument(content);

        // Assert
        Assert.True(result.Count > 1);
        
        // Check that the second chunk starts with words from the end of the first chunk
        // Note: This test is approximate since tokenization might not split exactly on words
        var firstChunkWords = result[0].Content.Split(' ');
        var secondChunkWords = result[1].Content.Split(' ');
        
        // The overlap should contain the last N words of the first chunk
        // where N is the overlap size (approximately)
        var overlapFound = false;
        for (int i = 1; i <= _mockOptions.Object.Value.ChunkOverlap && i < firstChunkWords.Length; i++)
        {
            if (secondChunkWords[0] == firstChunkWords[firstChunkWords.Length - i])
            {
                overlapFound = true;
                break;
            }
        }
        
        Assert.True(overlapFound, "Expected to find overlap between chunks");
    }
}
