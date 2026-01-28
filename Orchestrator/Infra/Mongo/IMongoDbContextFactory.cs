namespace Orchestrator.Infra.Mongo;

public interface IMongoDbContextFactory
{
    Task<IMongoDbContext> CreateAsync(string country);
}