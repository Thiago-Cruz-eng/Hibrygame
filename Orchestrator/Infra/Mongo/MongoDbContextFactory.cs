using MongoDB.Driver;

namespace Orchestrator.Infra.Mongo;

public class MongoDbContextFactory : IMongoDbContextFactory
{
    private readonly IConfiguration _configuration;

    public MongoDbContextFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IMongoDbContext> CreateAsync(string country)
    {
        var client = new MongoClient("mongodb://localhost:27017");
        return new MongoDbContext(client.GetDatabase("Hibrygame"));
    }
}