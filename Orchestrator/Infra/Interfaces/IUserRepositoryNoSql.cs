using Orchestrator.Domain;

namespace Orchestrator.Infra.Interfaces;

/// <summary>
/// Repositório de <see cref="User"/>, visto pelos casos de uso.
///
/// <para>
/// Não declara membro nenhum: herda as cinco operações de
/// <see cref="IGenericRepositoryNoSql{T}"/>. Existe para que o caso de uso dependa de "o
/// repositório de usuário" e não do genérico — assim o construtor diz de que entidade aquele
/// caso de uso trata, e um mock no teste substitui só a parte de usuário.
/// </para>
///
/// <para>
/// Query específica de usuário (por e-mail, por papel) se declara aqui e se implementa em
/// <c>UserRepositoryNoSql</c>.
/// </para>
/// </summary>
public interface IUserRepositoryNoSql : IGenericRepositoryNoSql<User>
{
}
