using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PathRAG.Core.Models;
using PathRAG.Core.Queries;
using System.Security.Claims;
using System.Text;

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

        var command = new RAGQuery
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
    [HttpPost("/stream")]
    public async Task<IActionResult> QueryStream(QueryRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var command = new RAGStreamQuery
        {
            Query = request.Query,
            VectorStoreIds = request.VectorStoreIds,
            SearchMode = (SearchMode)Enum.Parse(typeof(SearchMode), request.SearchMode, true),
            AssistantId = request.AssistantId,
            UserId = userId
        };

        try
        {
            // Create a cancellation token source that will be canceled when the client disconnects
            var cancellationTokenSource = new CancellationTokenSource();
            Response.HttpContext.RequestAborted.Register(() => cancellationTokenSource.Cancel());

            // Set up the response for streaming
            Response.Headers["Content-Type"] = "text/plain";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";

            // Get the stream generator from the handler
            var streamGenerator = await _mediator.Send(command, cancellationTokenSource.Token);

            // Stream the response
            await foreach (var chunk in streamGenerator.WithCancellation(cancellationTokenSource.Token))
            {
                await Response.Body.WriteAsync(
                    Encoding.UTF8.GetBytes(chunk),
                    0,
                    chunk.Length,
                    cancellationTokenSource.Token);
                await Response.Body.FlushAsync(cancellationTokenSource.Token);
            }

            return new EmptyResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Assistant not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming query response");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }
}
