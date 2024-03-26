using System.Linq.Expressions;

namespace Orchestrator.Infra.BaseRepository;

public interface IBaseRepositoryNoSql<TId, TEntity> where TEntity : class
{
    Task Save(TEntity entity);
    Task Update(TId id, TEntity entity);
    Task<List<TEntity>> FindAll();
    Task<List<TEntity>> FindByFilter(Expression<Func<TEntity, bool>> filter);
    void Delete(TId id, TEntity entity);
}