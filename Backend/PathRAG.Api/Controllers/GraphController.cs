using Microsoft.AspNetCore.Mvc;
using PathRAG.Core.Models;
using PathRAG.Core.Services.Graph;
using PathRAG.Infrastructure.Models;
using System.Threading;
using System.Threading.Tasks;

namespace PathRAG.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GraphController : ControllerBase
    {
        private readonly IGraphStorageService _graphStorageService;
        private readonly ILogger<GraphController> _logger;

        public GraphController(IGraphStorageService graphStorageService, ILogger<GraphController> logger)
        {
            _graphStorageService = graphStorageService;
            _logger = logger;
        }

        /// <summary>
        /// Gets graph data with entities and relationships
        /// </summary>
        /// <param name="label">Optional label to filter entities by type</param>
        /// <param name="maxDepth">Maximum depth of relationships to fetch</param>
        /// <param name="maxNodes">Maximum number of nodes to fetch</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Graph data with entities and relationships</returns>
        [HttpGet]
        public async Task<ActionResult<GraphData>> GetGraph(
            [FromQuery] string label = "*",
            [FromQuery] int maxDepth = 2,
            [FromQuery] int maxNodes = 100,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Fetching graph data with label: {Label}, maxDepth: {MaxDepth}, maxNodes: {MaxNodes}", 
                    label, maxDepth, maxNodes);
                
                var graphData = await _graphStorageService.GetGraphDataAsync(label, maxDepth, maxNodes, cancellationToken);
                return Ok(graphData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching graph data");
                return StatusCode(500, "An error occurred while fetching graph data");
            }
        }

        /// <summary>
        /// Gets all available entity labels/types
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Array of entity labels/types</returns>
        [HttpGet("labels")]
        public async Task<ActionResult<IEnumerable<string>>> GetLabels(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Fetching graph labels");
                var labels = await _graphStorageService.GetLabelsAsync(cancellationToken);
                return Ok(labels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching graph labels");
                return StatusCode(500, "An error occurred while fetching graph labels");
            }
        }

        /// <summary>
        /// Gets details for a specific entity
        /// </summary>
        /// <param name="id">ID of the entity to fetch</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Entity details</returns>
        [HttpGet("entity/{id}")]
        public async Task<ActionResult<GraphEntity>> GetEntity(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Fetching entity with ID: {Id}", id);
                var entity = await _graphStorageService.GetEntityAsync(id, cancellationToken);
                
                if (entity == null)
                {
                    return NotFound($"Entity with ID {id} not found");
                }
                
                return Ok(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching entity with ID: {Id}", id);
                return StatusCode(500, $"An error occurred while fetching entity with ID {id}");
            }
        }

        /// <summary>
        /// Gets relationships for a specific entity
        /// </summary>
        /// <param name="id">ID of the entity to fetch relationships for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Array of relationships</returns>
        [HttpGet("entity/{id}/relationships")]
        public async Task<ActionResult<IEnumerable<Relationship>>> GetEntityRelationships(
            string id, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Fetching relationships for entity with ID: {Id}", id);
                var relationships = await _graphStorageService.GetEntityRelationshipsAsync(id, cancellationToken);
                return Ok(relationships);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching relationships for entity with ID: {Id}", id);
                return StatusCode(500, $"An error occurred while fetching relationships for entity with ID {id}");
            }
        }

        /// <summary>
        /// Updates an entity's properties
        /// </summary>
        /// <param name="id">ID of the entity to update</param>
        /// <param name="entity">Updated entity data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Updated entity</returns>
        [HttpPut("entity/{id}")]
        public async Task<ActionResult<GraphEntity>> UpdateEntity(
            string id, 
            [FromBody] GraphEntity entity, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (id != entity.Id.ToString())
                {
                    return BadRequest("Entity ID in URL does not match entity ID in request body");
                }
                
                _logger.LogInformation("Updating entity with ID: {Id}", id);
                var updatedEntity = await _graphStorageService.UpdateEntityAsync(entity, cancellationToken);
                
                if (updatedEntity == null)
                {
                    return NotFound($"Entity with ID {id} not found");
                }
                
                return Ok(updatedEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating entity with ID: {Id}", id);
                return StatusCode(500, $"An error occurred while updating entity with ID {id}");
            }
        }

        /// <summary>
        /// Updates a relationship's properties
        /// </summary>
        /// <param name="id">ID of the relationship to update</param>
        /// <param name="relationship">Updated relationship data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Updated relationship</returns>
        [HttpPut("relationship/{id}")]
        public async Task<ActionResult<Relationship>> UpdateRelationship(
            string id, 
            [FromBody] Relationship relationship, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (id != relationship.Id.ToString())
                {
                    return BadRequest("Relationship ID in URL does not match relationship ID in request body");
                }
                
                _logger.LogInformation("Updating relationship with ID: {Id}", id);
                var updatedRelationship = await _graphStorageService.UpdateRelationshipAsync(relationship, cancellationToken);
                
                if (updatedRelationship == null)
                {
                    return NotFound($"Relationship with ID {id} not found");
                }
                
                return Ok(updatedRelationship);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating relationship with ID: {Id}", id);
                return StatusCode(500, $"An error occurred while updating relationship with ID {id}");
            }
        }

        /// <summary>
        /// Runs a PageRank algorithm on the graph
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>PageRank results for each node</returns>
        [HttpPost("pagerank")]
        public async Task<ActionResult<IDictionary<string, float>>> RunPageRank(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Running PageRank algorithm");
                var results = await _graphStorageService.RunPageRankAsync(cancellationToken);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running PageRank algorithm");
                return StatusCode(500, "An error occurred while running PageRank algorithm");
            }
        }

        /// <summary>
        /// Runs a Node2Vec embedding algorithm on the graph
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Node2Vec embeddings for each node</returns>
        [HttpPost("node2vec")]
        public async Task<ActionResult<float[]>> RunNode2Vec(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Running Node2Vec algorithm");
                var embeddings = await _graphStorageService.EmbedNodesAsync("node2vec", cancellationToken);
                return Ok(embeddings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running Node2Vec algorithm");
                return StatusCode(500, "An error occurred while running Node2Vec algorithm");
            }
        }
    }
}
