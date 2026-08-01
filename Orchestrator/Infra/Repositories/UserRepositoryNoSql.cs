using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.Infra.Mongo;

namespace Orchestrator.Infra.Repositories;

public class UserRepositoryNoSql : BaseRepositoryNoSql<User>, IUserRepositoryNoSql
{
    public UserRepositoryNoSql(IGenericRepository genericRepository) : base(genericRepository) { }
}
