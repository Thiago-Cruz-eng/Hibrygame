using System.ComponentModel.DataAnnotations;

namespace Orchestrator.UseCases.Dto.Request;

/// <summary>
/// Corpo de <c>POST /users/change-password</c>.
///
/// <para>
/// <b>O alvo é sempre quem está chamando.</b> O controller deriva o usuário do claim <c>sub</c> do
/// token e ignora <see cref="UserId"/> e <see cref="ModifiedBy"/>. Os dois campos continuam aqui
/// só para não quebrar clientes que já os enviam — enviar o id de outra pessoa não troca a senha
/// dela.
/// </para>
///
/// <para>
/// Exigir <see cref="CurrentPassword"/> continua sendo a segunda barreira: mesmo com uma sessão
/// aberta de outra pessoa, sem a senha atual não se toma a conta.
/// </para>
/// </summary>
public class ChangePasswordRequest
{
    /// <summary>
    /// <b>Ignorado.</b> O usuário alvo é sempre o do claim <c>sub</c> do token.
    ///
    /// <para>
    /// Era este campo que permitia a qualquer jogador autenticado trocar a senha de outro usuário,
    /// bastando saber a senha atual dele (era a DT-16). Mantido no DTO, sem <c>[Required]</c>,
    /// apenas para não quebrar o contrato do frontend: o servidor lê o id do token e nem olha
    /// para este valor.
    /// </para>
    /// </summary>
    [StringLength(64)]
    public string UserId { get; set; } = null!;

    /// <summary>Senha atual. Conferida contra o hash gravado antes de qualquer alteração.</summary>
    [Required, DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = null!;

    /// <summary>
    /// Senha nova. Ganha salt novo ao ser gravada — o anterior não é reaproveitado.
    ///
    /// <para>
    /// Mínimo de 8 caracteres e teto de 128, a mesma política do cadastro — sem isso, trocar a
    /// senha seria o caminho para contornar a exigência feita no cadastro.
    /// </para>
    /// </summary>
    [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 8)]
    public string NewPassword { get; set; } = null!;

    /// <summary>
    /// Repetição da senha nova. Comparada com <see cref="NewPassword"/> por nome — renomear aquela
    /// propriedade desliga esta conferência sem aviso do compilador.
    /// </summary>
    [Compare("NewPassword")]
    public string NewPasswordConfirmation { get; set; } = null!;

    /// <summary>
    /// <b>Ignorado.</b> A auditoria grava o claim <c>sub</c> de quem chamou.
    ///
    /// <para>
    /// Mantido no DTO pelo mesmo motivo de <see cref="UserId"/>: compatibilidade com o cliente
    /// atual. Auditoria que aceita o autor informado pelo próprio autor não é auditoria.
    /// </para>
    /// </summary>
    [StringLength(100)]
    public string ModifiedBy { get; set; } = null!;
}
