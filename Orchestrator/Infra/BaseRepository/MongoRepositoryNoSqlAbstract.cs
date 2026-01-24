using System.Linq.Expressions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Orchestrator.Infra.Mongo;

namespace Orchestrator.Infra.BaseRepository;

public abstract class MongoRepositoryNoSqlAbstract<TId, TEntity> : IBaseRepositoryNoSql<TId, TEntity> where TEntity : class
{
    private IMongoCollection<TEntity> _gameCollection;

    public MongoRepositoryNoSqlAbstract(IOptions<HibrygameDatabaseSettings> hibryDatabaseSettings)
    {
        var mongoClient = new MongoClient(
            hibryDatabaseSettings.Value.ConnectionString);

        var mongoDatabase = mongoClient.GetDatabase(
            hibryDatabaseSettings.Value.DatabaseName);

        var collectionName = ResolveCollectionName();
        _gameCollection = mongoDatabase.GetCollection<TEntity>(collectionName);
    }

    private static string ResolveCollectionName()
    {
        var attribute = typeof(TEntity).GetCustomAttributes(typeof(MongoCollectionAttribute), false)
            .Cast<MongoCollectionAttribute>()
            .FirstOrDefault();

        return attribute?.Name ?? typeof(TEntity).Name.ToLowerInvariant();
    }

    public Task Save(TEntity entity)
    {
        return _gameCollection.InsertOneAsync(entity);
    }

    public Task Update(TId id, TEntity entity)
    { 
        var filter = Builders<TEntity>.Filter.Eq("_id", id);
        return _gameCollection.ReplaceOneAsync(filter, entity);
    }

    public async Task<List<TEntity>> FindAll()
    {
        return await _gameCollection.Find(_ => true).ToListAsync();
    }

    public Task<List<TEntity>> FindByFilter(Expression<Func<TEntity, bool>> filter)
    {
        return _gameCollection.Find(filter).ToListAsync();
    }

    public void Delete(TId id, TEntity entity)
    {
        var filter = Builders<TEntity>.Filter.Eq("_id", id);
        _gameCollection.DeleteOneAsync(filter);
    }
}
