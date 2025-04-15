using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PathRAG.Api.Models;
using PathRAG.Core.Commands;
using System.Security.Claims;

namespace PathRAG.Api.Controllers;

[ApiController]
[Route("api/query")]
[Authorize]
public class QueryController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<QueryController> _logger;

    public QueryController(
        IMediator mediator,
        ILogger<QueryController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<QueryResponseDto>> Query(QueryRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        var command = new QueryCommand
        {
            Query = request.Query,
            VectorStoreIds = request.VectorStoreIds,
            SearchMode = (SearchMode)Enum.Parse(typeof(SearchMode), request.SearchMode, true),
            AssistantId = request.AssistantId,
            UserId = userId
        };
        
        try
        {
            var result = await _mediator.Send(command);
            
            var response = new QueryResponseDto
            {
                Answer = result.Answer,
                Sources = result.Sources
            };
            
            if (result.Entities != null && result.Entities.Count > 0)
            {
                response.Entities = result.Entities.Select(e => new GraphEntityDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    Type = e.Type,
                    Description = e.Description,
                    VectorStoreId = e.VectorStoreId
                }).ToList();
            }
            
            if (result.Relationships != null && result.Relationships.Count > 0)
            {
                response.Relationships = result.Relationships.Select(r => new RelationshipDto
                {
                    Id = r.Id,
                    SourceEntityId = r.SourceEntityId,
                    TargetEntityId = r.TargetEntityId,
                    Type = r.Type,
                    Description = r.Description,
                    VectorStoreId = r.VectorStoreId
                }).ToList();
            }
            
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Assistant not found");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query");
            return StatusCode(500, "Error processing query");
        }
    }
}
