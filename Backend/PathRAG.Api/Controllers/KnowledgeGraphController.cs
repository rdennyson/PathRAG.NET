using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PathRAG.Core.Commands;
using PathRAG.Core.Models;
using PathRAG.Infrastructure.Models;
using System.Security.Claims;

namespace PathRAG.Api.Controllers;

[ApiController]
[Route("api/knowledgegraph")]
[Authorize]
public class KnowledgeGraphController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<KnowledgeGraphController> _logger;

    public KnowledgeGraphController(
        IMediator mediator,
        ILogger<KnowledgeGraphController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet("entities/{vectorStoreId}")]
    public async Task<ActionResult<IEnumerable<GraphEntityDto>>> GetEntities(Guid vectorStoreId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            var query = new GetEntitiesByVectorStoreIdQuery
            {
                VectorStoreId = vectorStoreId,
                UserId = userId
            };

            var entities = await _mediator.Send(query);
            return Ok(entities);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Vector store not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving entities: {Message}", ex.Message);
            return StatusCode(500, "Error retrieving entities");
        }
    }

    [HttpGet("relationships/{vectorStoreId}")]
    public async Task<ActionResult<IEnumerable<RelationshipDto>>> GetRelationships(Guid vectorStoreId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            var query = new GetRelationshipsByVectorStoreIdQuery
            {
                VectorStoreId = vectorStoreId,
                UserId = userId
            };

            var relationships = await _mediator.Send(query);
            return Ok(relationships);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Vector store not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving relationships: {Message}", ex.Message);
            return StatusCode(500, "Error retrieving relationships");
        }
    }

    [HttpGet("entity/{entityId}/textchunks")]
    public async Task<ActionResult<IEnumerable<TextChunkDto>>> GetEntityTextChunks(Guid entityId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            var query = new GetEntityTextChunksQuery
            {
                EntityId = entityId,
                UserId = userId
            };

            var textChunks = await _mediator.Send(query);
            return Ok(textChunks);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Entity not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving text chunks: {Message}", ex.Message);
            return StatusCode(500, "Error retrieving text chunks");
        }
    }

    // Streaming endpoint removed in favor of standard API approach

    [HttpPost("generate")]
    public async Task<ActionResult<List<KnowledgeGraphNode>>> GenerateKnowledgeGraph([FromBody] GenerateKnowledgeGraphRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            // Create a command to generate the knowledge graph
            var command = new GenerateKnowledgeGraphCommand
            {
                Query = request.Query,
                MaxNodes = request.MaxNodes ?? 15,
                VectorStoreId = request.VectorStoreId,
                UserId = userId
            };

            // Send the command to the handler
            var nodes = await _mediator.Send(command);

            // Return the nodes
            return Ok(nodes);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Vector store not found: {Message}", ex.Message);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating knowledge graph: {Message}", ex.Message);
            return StatusCode(500, "Error generating knowledge graph");
        }
    }
}

public class GenerateKnowledgeGraphRequest
{
    public string Query { get; set; } = string.Empty;
    public int? MaxNodes { get; set; }
    public Guid? VectorStoreId { get; set; }
}
