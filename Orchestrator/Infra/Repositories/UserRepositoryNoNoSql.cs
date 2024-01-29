using Microsoft.Extensions.Options;
using Orchestrator.Domain;
using Orchestrator.Infra.BaseRepository;
using Orchestrator.Infra.Interfaces;
using Orchestrator.Infra.Mongo;

namespace Orchestrator.Infra.Repositories;

public class UserRepositoryNoNoSql : MongoRepositoryNoSqlAbstract<string, User>, IUserRepositoryNoSql
{
    public UserRepositoryNoNoSql(IOptions<HibrygameDatabaseSettings> hibrygameDatabaseSettings)
    {
    }
}