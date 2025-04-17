using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PathRAG.Core.Commands;
using PathRAG.Core.Models;
using PathRAG.Core.Queries;
using System.Security.Claims;

namespace PathRAG.Api.Controllers;

[ApiController]
[Route("api/assistants")]
[Authorize]
public class AssistantsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AssistantsController> _logger;

    public AssistantsController(
        IMediator mediator,
        ILogger<AssistantsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AssistantDto>>> GetAssistants()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var query = new GetAssistantsQuery { UserId = userId };
        var assistants = await _mediator.Send(query);

        var assistantDtos = assistants.Select(a => new AssistantDto
        {
            Id = a.Id,
            Name = a.Name,
            Message = a.Message,
            Temperature = a.Temperature,
            VectorStoreIds = a.AssistantVectorStores.Select(avs => avs.VectorStoreId).ToList(),
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        });

        return Ok(assistantDtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AssistantDto>> GetAssistant(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var query = new GetAssistantByIdQuery { Id = id, UserId = userId };
        var assistant = await _mediator.Send(query);

        if (assistant == null)
        {
            return NotFound();
        }

        var assistantDto = new AssistantDto
        {
            Id = assistant.Id,
            Name = assistant.Name,
            Message = assistant.Message,
            Temperature = assistant.Temperature,
            VectorStoreIds = assistant.AssistantVectorStores.Select(avs => avs.VectorStoreId).ToList(),
            CreatedAt = assistant.CreatedAt,
            UpdatedAt = assistant.UpdatedAt
        };

        return Ok(assistantDto);
    }

    [HttpPost]
    public async Task<ActionResult<AssistantDto>> CreateAssistant(CreateAssistantRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var command = new CreateAssistantCommand
        {
            Name = request.Name,
            Message = request.Message,
            Temperature = request.Temperature,
            VectorStoreIds = request.VectorStoreIds,
            UserId = userId
        };

        var assistant = await _mediator.Send(command);

        var assistantDto = new AssistantDto
        {
            Id = assistant.Id,
            Name = assistant.Name,
            Message = assistant.Message,
            Temperature = assistant.Temperature,
            VectorStoreIds = request.VectorStoreIds,
            CreatedAt = assistant.CreatedAt,
            UpdatedAt = assistant.UpdatedAt
        };

        return CreatedAtAction(nameof(GetAssistant), new { id = assistant.Id }, assistantDto);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<AssistantDto>> UpdateAssistant(Guid id, UpdateAssistantRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var command = new UpdateAssistantCommand
        {
            Id = id,
            Name = request.Name,
            Message = request.Message,
            Temperature = request.Temperature,
            VectorStoreIds = request.VectorStoreIds,
            UserId = userId
        };

        try
        {
            var assistant = await _mediator.Send(command);

            var assistantDto = new AssistantDto
            {
                Id = assistant.Id,
                Name = assistant.Name,
                Message = assistant.Message,
                Temperature = assistant.Temperature,
                VectorStoreIds = assistant.AssistantVectorStores.Select(avs => avs.VectorStoreId).ToList(),
                CreatedAt = assistant.CreatedAt,
                UpdatedAt = assistant.UpdatedAt
            };

            return Ok(assistantDto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteAssistant(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var command = new DeleteAssistantCommand
        {
            Id = id,
            UserId = userId
        };

        var result = await _mediator.Send(command);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }
}
