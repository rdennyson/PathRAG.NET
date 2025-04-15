using MediatR;
using Microsoft.AspNetCore.Mvc;
using PathRAG.Core.Commands;

namespace PathRAG.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly IMediator _mediator;

    public DocumentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] string content)
    {
        await _mediator.Send(new InsertDocumentCommand(content));
        return Ok();
    }

    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] string query)
    {
        var result = await _mediator.Send(new QueryDocumentCommand(query));
        return Ok(result);
    }
}