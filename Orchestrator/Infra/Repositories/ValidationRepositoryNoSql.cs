using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.Infra.Mongo;

namespace Orchestrator.Infra.Repositories;

public class ValidationRepositoryNoSql : BaseRepositoryNoSql<Validation>, IValidationRepositoryNoSql
{
    public ValidationRepositoryNoSql(IGenericRepository genericRepository) : base(genericRepository) { }
}
