namespace Orchestrator.Infra.BaseRepository;

public interface IBaseRepositoryNoSql<TId, TEntity> where TEntity : class
{
    Task Save(TEntity entity);
    Task Update(TId id, TEntity entity);
    Task<List<TEntity>> FindAll();
    Task<TEntity> FindById(TId id);
    void Delete(TId id, TEntity entity);
}