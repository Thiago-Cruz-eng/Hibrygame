using System.Linq.Expressions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Orchestrator.Infra.Mongo;

namespace Orchestrator.Infra.BaseRepository;

public abstract class MongoRepositoryNoSqlAbstract<TId, TEntity> : IBaseRepositoryNoSql<TId, TEntity> where TEntity : class
{
    private IMongoCollection<TEntity> _gameCollection;
    
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