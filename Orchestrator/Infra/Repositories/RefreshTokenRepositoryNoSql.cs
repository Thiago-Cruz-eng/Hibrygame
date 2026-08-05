using Orchestrator.Domain;
using Orchestrator.Infra.BaseRepository;
using Orchestrator.Infra.Interfaces;

namespace Orchestrator.Infra.Repositories;

/// <summary>
/// Repositório da entidade <see cref="RefreshToken"/>. Vazio pelo mesmo motivo que
/// <see cref="UserRepositoryNoSql"/>: o CRUD inteiro vem de
/// <see cref="BaseRepositoryNoSql{T}"/>.
///
/// <para>
/// Note que <c>RefreshTokenUseCase</c> busca o token por <c>FindByFilter</c> comparando o
/// <b>hash</b>, nunca o valor em claro — o valor original não existe no banco. Se algum dia
/// entrar aqui uma query de conveniência, ela precisa respeitar isso.
/// </para>
/// </summary>
public class RefreshTokenRepositoryNoSql : BaseRepositoryNoSql<RefreshToken>, IRefreshTokenRepositoryNoSql
{
    public RefreshTokenRepositoryNoSql(IGenericRepository genericRepository) : base(genericRepository) { }
}
