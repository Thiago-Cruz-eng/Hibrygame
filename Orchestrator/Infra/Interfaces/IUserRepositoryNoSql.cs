using Orchestrator.Domain;
using Orchestrator.Infra.BaseRepository;

namespace Orchestrator.Infra.Interfaces;

public interface IUserRepositoryNoSql : IBaseRepositoryNoSql<string, User>
{
}