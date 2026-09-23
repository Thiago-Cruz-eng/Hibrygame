using Orchestrator.Domain;
using Orchestrator.Infra.BaseRepository;
using Orchestrator.Infra.Interfaces;

namespace Orchestrator.Infra.Repositories;

/// <summary>
/// Repositório da entidade <see cref="User"/>.
///
/// <para>
/// O CRUD vem de <see cref="BaseRepositoryNoSql{T}"/>. O que mora aqui é a única operação que a
/// base não cobre: a atualização <b>parcial</b> da senha, que precisa de <c>$set</c> em campos
/// escolhidos em vez da substituição do documento inteiro.
/// </para>
///
/// <para>
/// Por isso esta classe recebe o <see cref="IGenericRepository"/> além de repassá-lo à base — é o
/// caminho previsto na nota de <see cref="BaseRepositoryNoSql{T}"/> para quando uma entidade
/// precisa de algo que o contrato estreito não oferece.
/// </para>
/// </summary>
public class UserRepositoryNoSql : BaseRepositoryNoSql<User>, IUserRepositoryNoSql
{
    private readonly IGenericRepository _genericRepository;

    public UserRepositoryNoSql(IGenericRepository genericRepository) : base(genericRepository)
    {
        _genericRepository = genericRepository;
    }

    /// <inheritdoc />
    public Task<bool> UpdatePassword(User user, CancellationToken cancellationToken = default)
    {
        // Materializa a auditoria antes de gravar: se ChangePassword algum dia deixar de
        // preenchê-la, a falha aparece aqui com mensagem clara em vez de escrever null no
        // documento.
        var modification = user.ModificationInformations
            ?? throw new InvalidOperationException(
                "UpdatePassword exige um usuario ja mutado por User.ChangePassword, que preenche " +
                "ModificationInformations.");

        return _genericRepository.Update<User>(
            existing => existing.Id == user.Id,
            cancellationToken,
            (x => x.PasswordHash, user.PasswordHash),
            (x => x.Salt, user.Salt),
            (x => x.MustChangePassword, user.MustChangePassword),
            // `!` no seletor: a propriedade e anulavel no dominio, mas aqui ela e apenas o nome do
            // campo a atualizar, nunca um valor lido.
            (x => x.ModificationInformations!, modification));
    }
}
