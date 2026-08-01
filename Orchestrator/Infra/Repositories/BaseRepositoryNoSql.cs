using System.Linq.Expressions;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.Infra.Mongo;

namespace Orchestrator.Infra.Repositories;

public abstract class BaseRepositoryNoSql<T> : IGenericRepositoryNoSql<T> where T : BaseEntity
{
    private readonly IGenericRepository _genericRepository;

    protected BaseRepositoryNoSql(IGenericRepository genericRepository)
    {
        _genericRepository = genericRepository;
    }

    public async Task<IEnumerable<T>> FindByFilter(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        => await _genericRepository.GetAll(filter, cancellationToken: cancellationToken);

    public Task<T?> GetById(Guid id, CancellationToken cancellationToken = default)
        => _genericRepository.GetFirstOrDefault<T>(x => x.Id == id, cancellationToken);

    public Task Save(T entity, CancellationToken cancellationToken = default)
        => _genericRepository.Save(entity, cancellationToken);

    public Task<bool> Update(string id, T entity, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var guid))
            return Task.FromResult(false);
        return _genericRepository.ReplaceOne<T>(x => x.Id == guid, entity, cancellationToken);
    }

    public Task<bool> Delete(string id, T entity, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var guid))
            return Task.FromResult(false);
        return _genericRepository.DeleteOne<T>(x => x.Id == guid, cancellationToken);
    }
}
