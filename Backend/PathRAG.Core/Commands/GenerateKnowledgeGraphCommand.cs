using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PathRAG.Core.Models;
using PathRAG.Core.Services;
using PathRAG.Core.Services.Embedding;
using PathRAG.Core.Services.Entity;
using PathRAG.Core.Services.Graph;
using PathRAG.Core.Services.Vector;
using PathRAG.Infrastructure.Data;
using System.Text.Json;
using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore;
using PathRAG.Infrastructure.Models;

namespace PathRAG.Core.Commands;

public class GenerateKnowledgeGraphCommand : IRequest<List<KnowledgeGraphNode>>
{
    public string Query { get; set; } = string.Empty;
    public int MaxNodes { get; set; } = 15;
    public Guid? VectorStoreId { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class GenerateKnowledgeGraphHandler : IRequestHandler<GenerateKnowledgeGraphCommand, List<KnowledgeGraphNode>>
{
    private readonly PathRagDbContext _dbContext;
    private readonly ILLMService _llmService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IGraphStorageService _graphStorage;
    private readonly OpenAIClient _openAIClient;
    private readonly PathRagOptions _options;
    private readonly ILogger<GenerateKnowledgeGraphHandler> _logger;

    private static readonly string[] _nodeColors = new[]
    {
        "#FFD6E0", "#FFEFCF", "#D4F0F0", "#CCE2CB", "#B6CFB6",
        "#97C1A9", "#CCB7AE", "#D6CFCB", "#EBD2B4", "#F1E8B8",
        "#FCFAFA", "#E8DDB5", "#D1C089", "#B3A369", "#997B66"
    };

    public GenerateKnowledgeGraphHandler(
        PathRagDbContext dbContext,
        ILLMService llmService,
        IEmbeddingService embeddingService,
        IVectorSearchService vectorSearchService,
        IGraphStorageService graphStorage,
        OpenAIClient openAIClient,
        IOptions<PathRagOptions> options,
        ILogger<GenerateKnowledgeGraphHandler> logger)
    {
        _dbContext = dbContext;
        _llmService = llmService;
        _embeddingService = embeddingService;
        _vectorSearchService = vectorSearchService;
        _graphStorage = graphStorage;
        _openAIClient = openAIClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<KnowledgeGraphNode>> Handle(
        GenerateKnowledgeGraphCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating knowledge graph for query: {Query}", request.Query);

        // Verify vector store exists and belongs to user if provided
        if (request.VectorStoreId.HasValue)
        {
            var vectorStore = await _dbContext.VectorStores
                .FirstOrDefaultAsync(vs => vs.Id == request.VectorStoreId && vs.UserId == request.UserId, cancellationToken);

            if (vectorStore == null)
            {
                throw new KeyNotFoundException($"Vector store with ID {request.VectorStoreId} not found");
            }
        }

        // Get relevant entities and relationships if vector store is provided
        List<PathRAG.Infrastructure.Models.GraphEntity> relevantEntities = new();
        List<PathRAG.Infrastructure.Models.Relationship> relevantRelationships = new();

        if (request.VectorStoreId.HasValue)
        {
            // Get query embedding
            var queryEmbedding = await _embeddingService.GetEmbeddingAsync(request.Query, cancellationToken);

            // Get relevant entities
            var entities = await _vectorSearchService.SearchEntitiesAsync(
                queryEmbedding,
                Math.Min(request.MaxNodes, 50), // Limit to 50 entities max
                cancellationToken,
                new List<Guid> { request.VectorStoreId.Value });

            relevantEntities = entities.ToList();

            // Get relationships between these entities
            var entityIds = relevantEntities.Select(e => e.Id.ToString()).ToList();
            relevantRelationships = await _dbContext.Relationships
                .Where(r =>
                    r.VectorStoreId == request.VectorStoreId.Value &&
                    entityIds.Contains(r.SourceEntityId) &&
                    entityIds.Contains(r.TargetEntityId))
                .ToListAsync(cancellationToken);
        }

        // Prepare system prompt for knowledge graph generation
        string systemPrompt = @"
        Generate a knowledge graph with Nodes and Edges, adhering to the following specifications:

        Important Guidelines:
        - No cycles should exist. A Node may be linked to another Node only once in the entire graph.
        - Strive for a balanced and aesthetically pleasing graph.
        - Prioritize clarity and legibility in the graph's design.
        - The graph should be structured to facilitate easy understanding of the relationships and hierarchy among Nodes.

        Node Specifications:
        1. Each Node should be connected to no more than 4 other Nodes, but at least 1.
        2. The total number of Nodes in the graph should be between 8 and 15.
        3. Nodes should be visually distinct with light pastel colors for backgrounds.
        4. Arrange Nodes in a clear, readable layout, with the primary Node at the center and others radiating outwards.
        5. Position nodes with x and y coordinates between 0 and 800.

        Edge Specifications:
        1. Edges represent relationships between source and target Nodes.
        2. No cycles should exist. A Node may be linked to another Node only once in the entire graph.
        3. The total number of Edges connected to a Node should not exceed 4.
        4. Ensure variation in stroke colors for Edges to denote different relationship types or categories.
        5. Edges sharing the same label should have identical colors.
        ";

        // Add context from existing entities and relationships if available
        if (relevantEntities.Any())
        {
            systemPrompt += "\nUse the following entities and relationships as a reference:\n\n";

            systemPrompt += "Entities:\n";
            foreach (var entity in relevantEntities.Take(20))
            {
                systemPrompt += $"- {entity.Name} (Type: {entity.Type}): {entity.Description}\n";
            }

            if (relevantRelationships.Any())
            {
                systemPrompt += "\nRelationships:\n";
                foreach (var rel in relevantRelationships.Take(30))
                {
                    var sourceEntity = relevantEntities.FirstOrDefault(e => e.Id.ToString() == rel.SourceEntityId);
                    var targetEntity = relevantEntities.FirstOrDefault(e => e.Id.ToString() == rel.TargetEntityId);

                    if (sourceEntity != null && targetEntity != null)
                    {
                        systemPrompt += $"- {sourceEntity.Name} {rel.Type} {targetEntity.Name}: {rel.Description}\n";
                    }
                }
            }
        }

        // Prepare user message
        string userMessage = $"Topic: {request.Query} - Generate a knowledge graph about this topic.";

        // Create chat messages
        var messages = new List<ChatRequestMessage>
        {
            new ChatRequestSystemMessage(systemPrompt),
            new ChatRequestUserMessage(userMessage)
        };

        // Create function definition for structured output
        var nodeFunction = new FunctionDefinition
        {
            Name = "create_node",
            Description = "Create a node in the knowledge graph",
            Parameters = BinaryData.FromString(@"
            {
                ""type"": ""object"",
                ""properties"": {
                    ""id"": {
                        ""type"": ""string"",
                        ""description"": ""Unique identifier for the Node.""
                    },
                    ""label"": {
                        ""type"": ""string"",
                        ""description"": ""Descriptive label or name of the Node.""
                    },
                    ""stroke"": {
                        ""type"": ""string"",
                        ""description"": ""Border color of the Node.""
                    },
                    ""background"": {
                        ""type"": ""string"",
                        ""description"": ""Background color of the Node.""
                    },
                    ""x"": {
                        ""type"": ""integer"",
                        ""description"": ""Horizontal position of the Node in the graph.""
                    },
                    ""y"": {
                        ""type"": ""integer"",
                        ""description"": ""Vertical position of the Node in the graph.""
                    },
                    ""adjacencies"": {
                        ""type"": ""array"",
                        ""items"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""target"": {
                                    ""type"": ""string"",
                                    ""description"": ""Unique identifier of the target Node.""
                                },
                                ""label"": {
                                    ""type"": ""string"",
                                    ""description"": ""Descriptive label of the Edge, indicating the relationship type.""
                                },
                                ""color"": {
                                    ""type"": ""string"",
                                    ""description"": ""Color code (e.g., hexadecimal) representing the Edge's visual color.""
                                }
                            },
                            ""required"": [""target"", ""label"", ""color""]
                        },
                        ""description"": ""List of Edges to other Nodes, defining the Node's connections.""
                    }
                },
                ""required"": [""id"", ""label"", ""background"", ""x"", ""y"", ""adjacencies""]
            }")
        };

        // Create chat completions options
        var chatCompletionsOptions = new ChatCompletionsOptions
        {
            DeploymentName = _options.DeploymentName,
            Temperature = 0.7f,
            MaxTokens = 4000,
            Functions = { nodeFunction },
            FunctionCall = FunctionDefinition.Auto
        };

        // Add messages
        foreach (var message in messages)
        {
            chatCompletionsOptions.Messages.Add(message);
        }

        // Create a random number generator for node positions
        var random = new Random();

        // Create a set to track node IDs
        var nodeIds = new HashSet<string>();
        var processedNodes = new HashSet<string>();

        // Instead of streaming, we'll generate a fixed set of nodes for now
        // This is a simplified implementation that doesn't use the OpenAI streaming API
        // In a real implementation, you would use the streaming API to get nodes in real-time

        // Create a list to hold our nodes
        var nodes = new List<KnowledgeGraphNode>();

        // Create a central node for the query
        var centralNode = new KnowledgeGraphNode
        {
            Id = "query",
            Label = request.Query,
            Background = "#FFD6E0",
            Stroke = "#000000",
            X = 400,
            Y = 400,
            Adjacencies = new List<KnowledgeGraphEdge>()
        };

        nodes.Add(centralNode);

        // Add nodes for each relevant entity
        int i = 0;
        foreach (var entity in relevantEntities.Take(Math.Min(request.MaxNodes - 1, 10)))
        {
            var nodeId = $"entity_{entity.Id}";
            var node = new KnowledgeGraphNode
            {
                Id = nodeId,
                Label = entity.Name,
                Background = _nodeColors[i % _nodeColors.Length],
                Stroke = "#000000",
                X = 200 + (i * 50) % 400,
                Y = 200 + (i * 50) % 400,
                Adjacencies = new List<KnowledgeGraphEdge>()
            };

            // Add an edge from the central node to this entity
            var edge = new KnowledgeGraphEdge
            {
                Id = $"query_{nodeId}",
                Source = "query",
                Target = nodeId,
                Label = entity.Type,
                Color = "#999999"
            };

            centralNode.Adjacencies.Add(edge);

            nodes.Add(node);
            i++;
        }

        // Add edges between entities based on relationships
        foreach (var relationship in relevantRelationships.Take(20))
        {
            var sourceId = $"entity_{relationship.SourceEntityId}";
            var targetId = $"entity_{relationship.TargetEntityId}";

            // Check if both source and target nodes exist
            var sourceNode = nodes.FirstOrDefault(n => n.Id == sourceId);
            var targetNode = nodes.FirstOrDefault(n => n.Id == targetId);

            if (sourceNode != null && targetNode != null)
            {
                var edge = new KnowledgeGraphEdge
                {
                    Id = $"{sourceId}_{targetId}",
                    Source = sourceId,
                    Target = targetId,
                    Label = relationship.Type,
                    Color = "#666666"
                };

                sourceNode.Adjacencies.Add(edge);
            }
        }

        // Return all nodes at once
        return nodes;
    }
}
