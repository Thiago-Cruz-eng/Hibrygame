using System.Linq.Expressions;
using Orchestrator.Domain;

namespace Orchestrator.Infra.Interfaces;

public interface IGenericRepositoryNoSql<T> where T : BaseEntity
{
    Task<IEnumerable<T>> FindByFilter(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);
    Task<T?> GetById(Guid id, CancellationToken cancellationToken = default);
    Task Save(T entity, CancellationToken cancellationToken = default);
    Task<bool> Update(string id, T entity, CancellationToken cancellationToken = default);
    Task<bool> Delete(string id, T entity, CancellationToken cancellationToken = default);
}
