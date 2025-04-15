namespace PathRAG.Tests
{
    using global::PathRAG.Core.Models;
    using global::PathRAG.Core.Services.Query;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Xunit;

    public class ContextBuilderServiceTests
    {
        private readonly ContextBuilderService _contextBuilderService;

        public ContextBuilderServiceTests()
        {
            _contextBuilderService = new ContextBuilderService();
        }

        [Fact]
        public async Task BuildContextAsync_WithAllData_ReturnsFormattedContext()
        {
            // Arrange
            var chunks = new List<TextChunk>
        {
            new TextChunk
            {
                Content = "This is a test chunk."
            }
        };

            var entities = new List<GraphEntity>
        {
            new GraphEntity
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Entity1",
                Type = "Person",
                Description = "This is entity 1"
            },
            new GraphEntity
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Entity2",
                Type = "Organization",
                Description = "This is entity 2"
            }
        };

            var relationships = new List<Relationship>
        {
            new Relationship
            {
                SourceEntityId = "11111111-1111-1111-1111-111111111111",
                TargetEntityId = "22222222-2222-2222-2222-222222222222",
                Type = "WORKS_FOR",
                Description = "Employment relationship"
            }
        };

            // Act
            var result = await _contextBuilderService.BuildContextAsync(chunks, entities, relationships);

            // Assert
            Assert.Contains("Relevant Text Passages:", result);
            Assert.Contains("- This is a test chunk.", result);
            Assert.Contains("Related Entities:", result);
            Assert.Contains("- Entity1 (Person): This is entity 1", result);
            Assert.Contains("- Entity2 (Organization): This is entity 2", result);
            Assert.Contains("Entity Relationships:", result);
            Assert.Contains("- Entity1 WORKS_FOR Entity2: Employment relationship", result);
        }

        [Fact]
        public async Task BuildContextAsync_WithMissingEntityInMap_UsesEntityId()
        {
            // Arrange
            var chunks = new List<TextChunk>();
            var entities = new List<GraphEntity>
        {
            new GraphEntity
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Entity1",
                Type = "Person",
                Description = "This is entity 1"
            }
        };

            var relationships = new List<Relationship>
        {
            new Relationship
            {
                SourceEntityId = "11111111-1111-1111-1111-111111111111",
                TargetEntityId = "33333333-3333-3333-3333-333333333333", // Not in entities list
                Type = "KNOWS",
                Description = "Knowledge relationship"
            }
        };

            // Act
            var result = await _contextBuilderService.BuildContextAsync(chunks, entities, relationships);

            // Assert
            Assert.Contains("Entity Relationships:", result);
            Assert.Contains("- Entity1 KNOWS 33333333-3333-3333-3333-333333333333: Knowledge relationship", result);
        }

        [Fact]
        public async Task BuildContextAsync_WithEmptyData_ReturnsEmptyString()
        {
            // Arrange
            var chunks = new List<TextChunk>();
            var entities = new List<GraphEntity>();
            var relationships = new List<Relationship>();

            // Act
            var result = await _contextBuilderService.BuildContextAsync(chunks, entities, relationships);

            // Assert
            Assert.Equal("", result);
        }
    }

}
