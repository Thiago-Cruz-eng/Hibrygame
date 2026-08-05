using Orchestrator.Domain;
using Orchestrator.Infra.BaseRepository;
using Orchestrator.Infra.Interfaces;

namespace Orchestrator.Infra.Repositories;

/// <summary>
/// Repositório da entidade <see cref="User"/>.
///
/// <para>
/// <b>Está vazio de propósito.</b> Todo o CRUD vem de
/// <see cref="BaseRepositoryNoSql{T}"/>; a classe existe para dar um tipo concreto ao
/// registro no <c>Program.cs</c> e um lugar óbvio para a primeira query que for específica de
/// usuário. Enquanto ninguém precisar de uma, ela continua assim.
/// </para>
/// </summary>
public class UserRepositoryNoSql : BaseRepositoryNoSql<User>, IUserRepositoryNoSql
{
    public UserRepositoryNoSql(IGenericRepository genericRepository) : base(genericRepository) { }
}
