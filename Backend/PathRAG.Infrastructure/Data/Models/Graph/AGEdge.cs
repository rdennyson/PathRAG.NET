using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Collections.Generic;

namespace PathRAG.Infrastructure.Models.Graph
{
    /// <summary>
    /// Represents an edge in the Apache AGE extension.
    /// </summary>
    [Table("ag_edge", Schema = "ag_catalog")]
    public class AGEdge
    {
        /// <summary>
        /// The name of the graph this edge belongs to.
        /// </summary>
        [Column("graph_name")]
        public string GraphName { get; set; }

        /// <summary>
        /// The ID of the start vertex.
        /// </summary>
        [Column("start_id")]
        public string StartId { get; set; }

        /// <summary>
        /// The ID of the end vertex.
        /// </summary>
        [Column("end_id")]
        public string EndId { get; set; }

        /// <summary>
        /// The label of the edge.
        /// </summary>
        [Column("label")]
        public string Label { get; set; }

        /// <summary>
        /// The properties of the edge as a JSON object.
        /// </summary>
        [Column("properties", TypeName = "jsonb")]
        public string PropertiesJson { get; set; }

        /// <summary>
        /// The graph this edge belongs to.
        /// </summary>
        [ForeignKey("GraphName")]
        public virtual AGGraph Graph { get; set; }

        /// <summary>
        /// The start vertex of the edge.
        /// </summary>
        [ForeignKey("GraphName,StartId")]
        public virtual AGVertex StartVertex { get; set; }

        /// <summary>
        /// The end vertex of the edge.
        /// </summary>
        [ForeignKey("GraphName,EndId")]
        public virtual AGVertex EndVertex { get; set; }

        /// <summary>
        /// Gets the properties of the edge as a dictionary.
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
