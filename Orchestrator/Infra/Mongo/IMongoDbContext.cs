using MongoDB.Driver;

namespace Orchestrator.Infra.Mongo;

public interface IMongoDbContext
{
    IMongoDatabase Database { get; }
}