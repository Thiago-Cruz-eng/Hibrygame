using Microsoft.Extensions.Options;
using Orchestrator.Domain;
using Orchestrator.Infra.BaseRepository;
using Orchestrator.Infra.Interfaces;
using Orchestrator.Infra.Mongo;

namespace Orchestrator.Infra.Repositories;

public class RefreshTokenRepositoryNoSql : MongoRepositoryNoSqlAbstract<string, RefreshToken>, IRefreshTokenRepositoryNoSql
{
    public RefreshTokenRepositoryNoSql(IOptions<HibrygameDatabaseSettings> hibrygameDatabaseSettings) : base(hibrygameDatabaseSettings)
    {
    }
}
