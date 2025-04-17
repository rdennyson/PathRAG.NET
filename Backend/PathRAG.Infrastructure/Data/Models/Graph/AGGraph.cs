using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PathRAG.Infrastructure.Models.Graph
{
    /// <summary>
    /// Represents a graph in the Apache AGE extension.
    /// </summary>
    [Table("ag_graph", Schema = "ag_catalog")]
    public class AGGraph
    {
        /// <summary>
        /// The name of the graph.
        /// </summary>
        [Key]
        [Column("name")]
        public string Name { get; set; }

        /// <summary>
        /// The timestamp when the graph was created.
        /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// The vertices (nodes) in the graph.
        /// </summary>
        public virtual ICollection<AGVertex> Vertices { get; set; } = new List<AGVertex>();

        /// <summary>
        /// The edges in the graph.
        /// </summary>
        public virtual ICollection<AGEdge> Edges { get; set; } = new List<AGEdge>();
    }
}
