using System.Linq.Expressions;
using MongoDB.Driver;
using Orchestrator.Domain;

namespace Orchestrator.Infra.Mongo;

public interface IGenericRepository
{
    IMongoCollection<T> GetCollection<T>() where T : BaseEntity;

    Task<List<T>> GetAll<T>(
        Expression<Func<T, bool>>? filter = null,
        int skip = 0,
        int limit = int.MaxValue,
        SortDefinition<T>? sort = null,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity;

    Task<T?> GetFirstOrDefault<T>(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity;

    Task<TDestination?> GetFirstProjectedOrDefault<T, TDestination>(
        Expression<Func<T, TDestination>> projection,
        Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellationToken = default) where T : BaseEntity;

    Task<IEnumerable<TDestination>> GetProjected<T, TDestination>(
        Expression<Func<T, TDestination>> projection,
        Expression<Func<T, bool>>? filter = null,
        int skip = 0,
        int limit = int.MaxValue,
        SortDefinition<T>? sort = null,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity;

    Task<IEnumerable<TDestination>> GetProjectedPaginated<T, TDestination>(
        Expression<Func<T, TDestination>> projection,
        Expression<Func<T, bool>>? filter = null,
        int page = 1,
        int pageSize = int.MaxValue,
        SortDefinition<T>? sort = null,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity;

    Task<long> CountAsync<T>(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity;

    Task<bool> Save<T>(T obj, CancellationToken cancellationToken = default) where T : BaseEntity;

    Task<T> SaveAndReturn<T>(T obj, CancellationToken cancellationToken = default) where T : BaseEntity;

    Task SaveMany<T>(List<T> listObj, CancellationToken cancellationToken = default) where T : BaseEntity;

    Task<bool> ReplaceOne<T>(Expression<Func<T, bool>> filter, T record,
        CancellationToken cancellationToken = default) where T : BaseEntity;

    Task<T> SaveOrReplaceOne<T>(Expression<Func<T, bool>> filter, T obj, CancellationToken cancellationToken = default) where T : BaseEntity;

    Task<bool> DeleteOne<T>(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        where T : BaseEntity;

    Task<bool> DeleteMany<T>(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        where T : BaseEntity;

    Task<bool> HasRecord<T>(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        where T : BaseEntity;

    Task<bool> Update<T>(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default,
        params (Expression<Func<T, object>>, object)[] updateDefinitions
    ) where T : BaseEntity;
}