using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.Infra.Mongo;

namespace Orchestrator.Infra.Repositories;

public class RefreshTokenRepositoryNoSql : BaseRepositoryNoSql<RefreshToken>, IRefreshTokenRepositoryNoSql
{
    public RefreshTokenRepositoryNoSql(IGenericRepository genericRepository) : base(genericRepository) { }
}
