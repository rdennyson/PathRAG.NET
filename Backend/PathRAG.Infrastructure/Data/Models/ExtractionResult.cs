namespace PathRAG.Infrastructure.Models;

public class ExtractionResult
{
    public List<GraphEntity> Entities { get; set; } = new();
    public List<Relationship> Relationships { get; set; } = new();
    public Dictionary<string, float> EntityWeights { get; set; } = new();
    public Dictionary<string, string> EntityDescriptions { get; set; } = new();
    public Dictionary<string, string> EntityKeywords { get; set; } = new();
    public Dictionary<string, string> EntityTypes { get; set; } = new();
}

//public class Entity
//{
//    public string Id { get; set; }
//    public string Name { get; set; }
//    public string Type { get; set; }
//    public string Description { get; set; }
//    public List<string> Keywords { get; set; } = new();
//    public float Weight { get; set; }
//    public string SourceId { get; set; }
//}

//public class Relationship
//{
//    public string SourceEntityId { get; set; }
//    public string TargetEntityId { get; set; }
//    public string Type { get; set; }
//    public string Description { get; set; }
//    public float Weight { get; set; }
//    public List<string> Keywords { get; set; } = new();
//    public string SourceId { get; set; }
//}