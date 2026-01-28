using System.Linq.Expressions;
using System.Reflection;
using MongoDB.Driver;
using Orchestrator.Domain;
using Orchestrator.Infra.Mongo;
using Orchestrator.Infra.Utils;

namespace Orchestrator.Infra.BaseRepository;

public class GenericRepository : IGenericRepository
{
    private IMongoDbContext _context;

    public IMongoCollection<T> GetCollection<T>() where T : BaseEntity
    {
        var collectionName = ResolveCollectionName<T>();
        return _context.Database.GetCollection<T>(collectionName);
    }

    private string ResolveCollectionName<T>() where T : BaseEntity
    {
        var name = typeof(T).Name;
        var entityName =
            (CollectionNameAttribute?)typeof(T).GetCustomAttributes().FirstOrDefault(a => a is CollectionNameAttribute);
        if (entityName != null)
            name = entityName.CollectionName;

        return name;
    }

    public virtual async Task<List<T>> GetAll<T>(
        Expression<Func<T, bool>>? filter = null,
        int skip = 0,
        int limit = int.MaxValue,
        SortDefinition<T>? sort = null,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity
    {
        filter ??= x => true;
        var data = await GetCollection<T>()
            .FindAsync(
                filter,
                FindOptionsExtensions.MakeFindOptions(limit, skip, sort),
                cancellationToken);

        return await data.ToListAsync();
    }

    public virtual async Task<T?> GetFirstOrDefault<T>(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity
    {
        var document = await GetCollection<T>()
            .Aggregate()
            .Match(filter)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        return document;
    }

    public virtual async Task<TDestination?> GetFirstProjectedOrDefault<T, TDestination>(
        Expression<Func<T, TDestination>> projection,
        Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellationToken = default) where T : BaseEntity
    {
        filter ??= x => true;
        var projectionBuilder = Builders<T>.Projection;
        var projectionDefinition = projectionBuilder.Expression(projection);
        var cursor = await GetCollection<T>().FindAsync(filter,
            options: new FindOptions<T, TDestination> { Projection = projectionDefinition },
            cancellationToken: cancellationToken);

        return await cursor.FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<TDestination>> GetProjected<T, TDestination>(
        Expression<Func<T, TDestination>> projection,
        Expression<Func<T, bool>>? filter = null,
        int skip = 0,
        int limit = int.MaxValue,
        SortDefinition<T>? sort = null,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity
    {
        return await GetCollection<T>()
            .GetProjected(
                projection,
                filter,
                skip,
                limit,
                sort,
                cancellationToken
            );
    }

    public virtual async Task<IEnumerable<TDestination>> GetProjectedPaginated<T, TDestination>(
        Expression<Func<T, TDestination>> projection,
        Expression<Func<T, bool>>? filter = null,
        int page = 1,
        int pageSize = int.MaxValue,
        SortDefinition<T>? sort = null,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity
    {
        return await GetProjected(
            projection,
            filter,
            skip: (page - 1) * pageSize,
            limit: pageSize,
            sort,
            cancellationToken
        );
    }

    public virtual async Task<long> CountAsync<T>(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity
    {
        var count = await GetCollection<T>().CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        return count;
    }

    public virtual async Task<bool> Save<T>(T obj, CancellationToken cancellationToken = default) where T : BaseEntity
    {
        await GetCollection<T>().InsertOneAsync(obj, cancellationToken: cancellationToken);
        return true;
    }

    public virtual async Task<T> SaveAndReturn<T>(T obj, CancellationToken cancellationToken = default) where T : BaseEntity
    {
        await GetCollection<T>().InsertOneAsync(obj, cancellationToken: cancellationToken);
        return obj;
    }


    public virtual async Task SaveMany<T>(List<T> listObj, CancellationToken cancellationToken = default)
        where T : BaseEntity
    {
        await GetCollection<T>().InsertManyAsync(listObj, cancellationToken: cancellationToken);
    }

    public virtual async Task<bool> ReplaceOne<T>(Expression<Func<T, bool>> filter, T record,
        CancellationToken cancellationToken = default) where T : BaseEntity
    {
        var result = await GetCollection<T>().ReplaceOneAsync(filter, record, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public virtual async Task<T> SaveOrReplaceOne<T>(Expression<Func<T, bool>> filter, T obj, CancellationToken cancellationToken = default) where T : BaseEntity
    {
        var replaced = await ReplaceOne(filter, obj, cancellationToken);
        if (replaced)
            return obj;

        await Save(obj, cancellationToken);
        return obj;
    }

    public virtual async Task<bool> DeleteOne<T>(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default) where T : BaseEntity
    {
        var deletedCount = (await GetCollection<T>().DeleteOneAsync(filter, cancellationToken)).DeletedCount;
        if (deletedCount > 0)
            return true;

        return false;
    }

    public virtual async Task<bool> DeleteMany<T>(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default) where T : BaseEntity
    {
        var deletedCount = (await GetCollection<T>().DeleteManyAsync(filter, cancellationToken)).DeletedCount;
        if (deletedCount > 0)
            return true;

        return false;
    }

    public virtual async Task<bool> HasRecord<T>(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default) where T : BaseEntity
    {
        var count = await CountAsync(filter, cancellationToken);
        return count > 0;
    }

    public virtual async Task<bool> Update<T>(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default,
        params (Expression<Func<T, object>>, object)[] updateDefinitions
    ) where T : BaseEntity
    {
        var update = Builders<T>.Update;
        var definitions = updateDefinitions.Aggregate(
            (UpdateDefinition<T>?)null,
            (current, ud) => current?.Set(ud.Item1, ud.Item2) ?? update.Set(ud.Item1, ud.Item2)
        );

        var updateResult = await GetCollection<T>()
            .UpdateOneAsync(
                filter,
                definitions, cancellationToken: cancellationToken);

        return updateResult.ModifiedCount > 0;
    }
}