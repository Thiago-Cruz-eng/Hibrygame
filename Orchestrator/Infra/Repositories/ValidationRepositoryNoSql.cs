using Microsoft.Extensions.Options;
using Orchestrator.Domain;
using Orchestrator.Infra.BaseRepository;
using Orchestrator.Infra.Interfaces;
using Orchestrator.Infra.Mongo;

namespace Orchestrator.Infra.Repositories;

public class ValidationRepositoryNoSql : MongoRepositoryNoSqlAbstract<string, Validation>, IValidationRepositoryNoSql
{
    public ValidationRepositoryNoSql(IOptions<HibrygameDatabaseSettings> hibrygameDatabaseSettings) : base(hibrygameDatabaseSettings)
    {
    }
}