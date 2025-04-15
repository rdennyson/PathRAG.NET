using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PathRAG.Api.Models;
using PathRAG.Core.Commands;
using PathRAG.Core.Models;
using PathRAG.Core.Queries;
using System.Security.Claims;

namespace PathRAG.Api.Controllers;

[ApiController]
[Route("api/chats")]
[Authorize]
public class ChatsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ChatsController> _logger;

    public ChatsController(
        IMediator mediator,
        ILogger<ChatsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ChatSessionDto>>> GetChatSessions()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var query = new GetChatSessionsQuery { UserId = userId };
        var chatSessions = await _mediator.Send(query);

        var chatSessionDtos = chatSessions.Select(cs => new ChatSessionDto
        {
            Id = cs.Id,
            Title = cs.Title,
            AssistantId = cs.AssistantId,
            CreatedAt = cs.CreatedAt,
            UpdatedAt = cs.UpdatedAt
        });

        return Ok(chatSessionDtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ChatSessionDetailDto>> GetChatSession(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var query = new GetChatSessionByIdQuery { Id = id, UserId = userId };
        var chatSession = await _mediator.Send(query);

        if (chatSession == null)
        {
            return NotFound();
        }

        var chatSessionDetailDto = new ChatSessionDetailDto
        {
            Id = chatSession.Id,
            Title = chatSession.Title,
            AssistantId = chatSession.AssistantId,
            Messages = chatSession.Messages.OrderBy(m => m.Timestamp).Select(m => new ChatMessageDto
            {
                Id = m.Id,
                Content = m.Content,
                Role = m.Role,
                Timestamp = m.Timestamp,
                Attachments = m.Attachments.Select(a => a.FileName).ToList()
            }).ToList(),
            CreatedAt = chatSession.CreatedAt,
            UpdatedAt = chatSession.UpdatedAt
        };

        return Ok(chatSessionDetailDto);
    }

    [HttpPost]
    public async Task<ActionResult<ChatSessionDto>> CreateChatSession(CreateChatSessionRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var command = new CreateChatSessionCommand
        {
            AssistantId = request.AssistantId,
            UserId = userId
        };

        try
        {
            var chatSession = await _mediator.Send(command);

            var chatSessionDto = new ChatSessionDto
            {
                Id = chatSession.Id,
                Title = chatSession.Title,
                AssistantId = chatSession.AssistantId,
                CreatedAt = chatSession.CreatedAt,
                UpdatedAt = chatSession.UpdatedAt
            };

            return CreatedAtAction(nameof(GetChatSession), new { id = chatSession.Id }, chatSessionDto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Assistant not found");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteChatSession(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var command = new DeleteChatSessionCommand
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

    [HttpPost("{id}/messages")]
    public async Task<ActionResult<ChatMessageDto>> AddChatMessage(Guid id, [FromForm] AddChatMessageRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var command = new AddChatMessageCommand
        {
            ChatSessionId = id,
            Content = request.Content,
            Role = request.Role,
            Attachments = request.Attachments,
            UserId = userId
        };

        try
        {
            var chatMessage = await _mediator.Send(command);

            var chatMessageDto = new ChatMessageDto
            {
                Id = chatMessage.Id,
                Content = chatMessage.Content,
                Role = chatMessage.Role,
                Timestamp = chatMessage.Timestamp,
                Attachments = chatMessage.Attachments.Select(a => a.FileName).ToList()
            };

            return Ok(chatMessageDto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Chat session not found");
        }
    }
}
