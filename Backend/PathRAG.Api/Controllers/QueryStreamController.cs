using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PathRAG.Api.Models;
using PathRAG.Core.Commands;
using PathRAG.Core.Models;
using System.Security.Claims;
using System.Text;

namespace PathRAG.Api.Controllers;

[ApiController]
[Route("api/query/stream")]
[Authorize]
public class QueryStreamController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<QueryStreamController> _logger;

    public QueryStreamController(
        IMediator mediator,
        ILogger<QueryStreamController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> QueryStream(QueryRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        var command = new StreamQueryCommand
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
            Response.Headers.Add("Content-Type", "text/plain");
            Response.Headers.Add("Cache-Control", "no-cache");
            Response.Headers.Add("Connection", "keep-alive");
            
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
