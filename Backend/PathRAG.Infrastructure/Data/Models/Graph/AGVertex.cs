using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace PathRAG.Infrastructure.Models.Graph
{
    /// <summary>
    /// Represents a vertex (node) in the Apache AGE extension.
    /// </summary>
    [Table("ag_vertex", Schema = "ag_catalog")]
    public class AGVertex
    {
        /// <summary>
        /// The name of the graph this vertex belongs to.
        /// </summary>
        [Column("graph_name")]
        public string GraphName { get; set; }

        /// <summary>
        /// The ID of the vertex.
        /// </summary>
        [Column("id")]
        public string Id { get; set; }

        /// <summary>
        /// The label of the vertex.
        /// </summary>
        [Column("label")]
        public string Label { get; set; }

        /// <summary>
        /// The properties of the vertex as a JSON object.
        /// </summary>
        [Column("properties", TypeName = "jsonb")]
        public string PropertiesJson { get; set; }

        /// <summary>
        /// The graph this vertex belongs to.
        /// </summary>
        [ForeignKey("GraphName")]
        public virtual AGGraph Graph { get; set; }

        /// <summary>
        /// The outgoing edges from this vertex.
        /// </summary>
        [InverseProperty("StartVertex")]
        public virtual ICollection<AGEdge> OutgoingEdges { get; set; } = new List<AGEdge>();

        /// <summary>
        /// The incoming edges to this vertex.
        /// </summary>
        [InverseProperty("EndVertex")]
        public virtual ICollection<AGEdge> IncomingEdges { get; set; } = new List<AGEdge>();

        /// <summary>
        /// Gets the properties of the vertex as a dictionary.
        /// </summary>
        [NotMapped]
        public Dictionary<string, object> Properties
        {
            get => string.IsNullOrEmpty(PropertiesJson) 
                ? new Dictionary<string, object>() 
                : JsonSerializer.Deserialize<Dictionary<string, object>>(PropertiesJson);
            set => PropertiesJson = JsonSerializer.Serialize(value);
        }
    }
}
