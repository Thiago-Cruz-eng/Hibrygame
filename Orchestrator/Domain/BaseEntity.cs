using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Orchestrator.Domain;

public class BaseEntity
{
    /// <summary>
    /// Unique identifier for the entity.
    /// </summary>
    [BsonIgnoreIfDefault]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;
}