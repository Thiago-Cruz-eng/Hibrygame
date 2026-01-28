using MongoDB.Driver;

namespace Orchestrator.Infra.Mongo;

public class MongoDbContext : IMongoDbContext
{
    public IMongoDatabase Database { get; }

    public MongoDbContext(IMongoDatabase database)
    {
        Database = database;
    }
}