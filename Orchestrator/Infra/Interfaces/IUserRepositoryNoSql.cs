using Orchestrator.Domain;

namespace Orchestrator.Infra.Interfaces;

/// <summary>
/// Repositório de <see cref="User"/>, visto pelos casos de uso.
///
/// <para>
/// Herda as cinco operações de <see cref="IGenericRepositoryNoSql{T}"/> e acrescenta uma:
/// <see cref="UpdatePassword"/>. Existe para que o caso de uso dependa de "o repositório de
/// usuário" e não do genérico — assim o construtor diz de que entidade aquele caso de uso trata,
/// e um mock no teste substitui só a parte de usuário.
/// </para>
///
/// <para>
/// Query específica de usuário (por e-mail, por papel) se declara aqui e se implementa em
/// <c>UserRepositoryNoSql</c>.
/// </para>
/// </summary>
public interface IUserRepositoryNoSql : IGenericRepositoryNoSql<User>
{
    /// <summary>
    /// Grava <b>apenas</b> os campos de senha e a auditoria da alteração.
    ///
    /// <para>
    /// <b>Por que não usar <c>Update(id, entity)</c>:</b> aquele substitui o documento inteiro, e
    /// entre a leitura do usuário e a gravação alguém pode ter mudado outro campo — nome, papel,
    /// vínculos. A substituição desfaria essa mudança sem que ninguém percebesse. Aqui só os
    /// quatro campos listados são tocados.
    /// </para>
    ///
    /// <para>
    /// Era o único motivo de <c>ChangePasswordUseCase</c> injetar <c>IGenericRepository</c>
    /// direto, furando a fronteira do Princípio I. Era a DT-19.
    /// </para>
    /// </summary>
    /// <param name="user">
    /// O usuário <b>já mutado</b> por <c>User.ChangePassword</c> — dele saem o hash novo, o salt
    /// novo, <c>MustChangePassword</c> e <c>ModificationInformations</c>.
    /// </param>
    /// <returns>
    /// <c>false</c> quando nada casou com o id <b>ou</b> quando os valores enviados já eram os
    /// gravados. Ver a nota de retorno em <c>IGenericRepository.Update</c>.
    /// </returns>
    Task<bool> UpdatePassword(User user, CancellationToken cancellationToken = default);
}
