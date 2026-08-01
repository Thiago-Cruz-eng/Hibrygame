using MongoDB.Bson.Serialization.Attributes;

namespace Orchestrator.Domain;

public class BaseEntity
{
    [BsonId]
    [BsonIgnoreIfDefault]
    public Guid Id { get; set; } = Guid.NewGuid();
}
