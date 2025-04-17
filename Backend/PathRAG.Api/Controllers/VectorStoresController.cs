using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PathRAG.Core.Commands;

using PathRAG.Core.Models;
using PathRAG.Core.Queries;
using System.Security.Claims;

namespace PathRAG.Api.Controllers;

[ApiController]
[Route("api/vectorstores")]
[Authorize]
public class VectorStoresController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<VectorStoresController> _logger;

    public VectorStoresController(
        IMediator mediator,
        ILogger<VectorStoresController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VectorStoreDto>>> GetVectorStores()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var query = new GetVectorStoresQuery { UserId = userId };
        var vectorStores = await _mediator.Send(query);

        var vectorStoreDtos = vectorStores.Select(vs => new VectorStoreDto
        {
            Id = vs.Id,
            Name = vs.Name,
            DocumentCount = vs.TextChunks.Count,
            CreatedAt = vs.CreatedAt
        });

        return Ok(vectorStoreDtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VectorStoreDto>> GetVectorStore(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var query = new GetVectorStoreByIdQuery { Id = id, UserId = userId };
        var vectorStore = await _mediator.Send(query);

        if (vectorStore == null)
        {
            return NotFound();
        }

        var vectorStoreDto = new VectorStoreDto
        {
            Id = vectorStore.Id,
            Name = vectorStore.Name,
            DocumentCount = vectorStore.TextChunks.Count,
            CreatedAt = vectorStore.CreatedAt
        };

        return Ok(vectorStoreDto);
    }

    [HttpPost]
    public async Task<ActionResult<VectorStoreDto>> CreateVectorStore(CreateVectorStoreRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var command = new CreateVectorStoreCommand
        {
            Name = request.Name,
            UserId = userId
        };

        var vectorStore = await _mediator.Send(command);

        var vectorStoreDto = new VectorStoreDto
        {
            Id = vectorStore.Id,
            Name = vectorStore.Name,
            DocumentCount = 0,
            CreatedAt = vectorStore.CreatedAt
        };

        return CreatedAtAction(nameof(GetVectorStore), new { id = vectorStore.Id }, vectorStoreDto);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteVectorStore(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var command = new DeleteVectorStoreCommand
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

    [HttpPost("{id}/documents")]
    public async Task<ActionResult<DocumentDto>> UploadDocument(Guid id, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var command = new UploadDocumentCommand
        {
            VectorStoreId = id,
            File = file,
            UserId = userId
        };

        try
        {
            var chunks = await _mediator.Send(command);

            // Get the document details
            var query = new GetDocumentByIdQuery
            {
                Id = Guid.Parse(chunks[0].FullDocumentId),
                UserId = userId
            };

            var document = await _mediator.Send(query);
            return Ok(document);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Vector store not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document: {Message}", ex.Message);
            return StatusCode(500, "Error processing document");
        }
    }

    [HttpGet("{id}/documents")]
    public async Task<ActionResult<IEnumerable<DocumentDto>>> GetDocuments(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            var query = new GetDocumentsByVectorStoreIdQuery
            {
                VectorStoreId = id,
                UserId = userId
            };

            var documents = await _mediator.Send(query);
            return Ok(documents);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Vector store not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents: {Message}", ex.Message);
            return StatusCode(500, "Error retrieving documents");
        }
    }

    [HttpGet("{vectorStoreId}/documents/{id}")]
    public async Task<ActionResult<DocumentDto>> GetDocument(Guid vectorStoreId, Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            var query = new GetDocumentByIdQuery
            {
                Id = id,
                UserId = userId
            };

            var document = await _mediator.Send(query);
            if (document == null)
            {
                return NotFound("Document not found");
            }

            return Ok(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document: {Message}", ex.Message);
            return StatusCode(500, "Error retrieving document");
        }
    }

    [HttpDelete("{vectorStoreId}/documents/{id}")]
    public async Task<ActionResult> DeleteDocument(Guid vectorStoreId, Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            var command = new DeleteDocumentCommand
            {
                Id = id,
                UserId = userId
            };

            var result = await _mediator.Send(command);
            if (!result)
            {
                return NotFound("Document not found");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document: {Message}", ex.Message);
            return StatusCode(500, "Error deleting document");
        }
    }

    [HttpGet("{id}/entities")]
    public async Task<ActionResult<IEnumerable<GraphEntityDto>>> GetEntities(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var query = new GetEntitiesByVectorStoreIdQuery { VectorStoreId = id, UserId = userId };
        var entities = await _mediator.Send(query);

        if (!entities.Any())
        {
            // Check if vector store exists
            var vectorStoreQuery = new GetVectorStoreByIdQuery { Id = id, UserId = userId };
            var vectorStore = await _mediator.Send(vectorStoreQuery);

            if (vectorStore == null)
            {
                return NotFound();
            }
        }

        var entityDtos = entities.Select(e => new GraphEntityDto
        {
            Id = e.Id,
            Name = e.Name,
            Type = e.Type,
            Description = e.Description,
            VectorStoreId = e.VectorStoreId
        });

        return Ok(entityDtos);
    }

    [HttpGet("{id}/relationships")]
    public async Task<ActionResult<IEnumerable<RelationshipDto>>> GetRelationships(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var query = new GetRelationshipsByVectorStoreIdQuery { VectorStoreId = id, UserId = userId };
        var relationships = await _mediator.Send(query);

        if (!relationships.Any())
        {
            // Check if vector store exists
            var vectorStoreQuery = new GetVectorStoreByIdQuery { Id = id, UserId = userId };
            var vectorStore = await _mediator.Send(vectorStoreQuery);

            if (vectorStore == null)
            {
                return NotFound();
            }
        }

        var relationshipDtos = relationships.Select(r => new RelationshipDto
        {
            Id = r.Id,
            SourceEntityId = r.SourceEntityId,
            TargetEntityId = r.TargetEntityId,
            Type = r.Type,
            Description = r.Description,
            VectorStoreId = r.VectorStoreId
        });

        return Ok(relationshipDtos);
    }
}
