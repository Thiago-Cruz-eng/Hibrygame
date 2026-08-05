using Orchestrator.Domain;
using Orchestrator.Infra.BaseRepository;
using Orchestrator.Infra.Interfaces;

namespace Orchestrator.Infra.Repositories;

/// <summary>
/// Repositório da entidade <see cref="Validation"/>. Vazio, como os outros: o CRUD vem de
/// <see cref="BaseRepositoryNoSql{T}"/>.
/// </summary>
public class ValidationRepositoryNoSql : BaseRepositoryNoSql<Validation>, IValidationRepositoryNoSql
{
    public ValidationRepositoryNoSql(IGenericRepository genericRepository) : base(genericRepository) { }
}
