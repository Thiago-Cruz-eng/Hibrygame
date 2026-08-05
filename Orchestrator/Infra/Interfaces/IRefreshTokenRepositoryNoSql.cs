using Orchestrator.Domain;

namespace Orchestrator.Infra.Interfaces;

/// <summary>
/// Repositório de <see cref="RefreshToken"/>, visto pelos casos de uso. Sem membro próprio —
/// ver a explicação em <see cref="IUserRepositoryNoSql"/>.
/// </summary>
public interface IRefreshTokenRepositoryNoSql : IGenericRepositoryNoSql<RefreshToken>
{
}
