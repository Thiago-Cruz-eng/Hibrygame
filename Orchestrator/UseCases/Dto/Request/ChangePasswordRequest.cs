using System.ComponentModel.DataAnnotations;

namespace Orchestrator.UseCases.Dto.Request;

/// <summary>
/// Corpo de <c>POST /users/change-password</c>.
///
/// <para>
/// Exigir <see cref="CurrentPassword"/> é o que impede que alguém com uma sessão aberta de outra
/// pessoa troque a senha dela e tome a conta. É a única barreira real neste fluxo: o servidor
/// <b>não</b> confere se <see cref="UserId"/> é o mesmo do token (DT-16).
/// </para>
/// </summary>
public class ChangePasswordRequest
{
    /// <summary>
    /// Usuário cuja senha será trocada.
    ///
    /// <para>
    /// <b>Não é conferido contra o token.</b> Quem souber a senha atual de alguém pode trocá-la por
    /// aqui — o que a senha atual já permitiria de todo modo, e é por isso que o dano é limitado.
    /// Ainda assim, a comparação com o claim <c>sub</c> deveria existir, como
    /// <c>ValidationController</c> faz.
    /// </para>
    /// </summary>
    [Required]
    public string UserId { get; set; } = null!;

    /// <summary>Senha atual. Conferida contra o hash gravado antes de qualquer alteração.</summary>
    [Required, DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = null!;

    /// <summary>
    /// Senha nova. Ganha salt novo ao ser gravada — o anterior não é reaproveitado.
    ///
    /// <para>
    /// Não há exigência de tamanho nem de complexidade, aqui nem no caso de uso: qualquer texto não
    /// vazio é aceito.
    /// </para>
    /// </summary>
    [Required, DataType(DataType.Password)]
    public string NewPassword { get; set; } = null!;

    /// <summary>
    /// Repetição da senha nova. Comparada com <see cref="NewPassword"/> por nome — renomear aquela
    /// propriedade desliga esta conferência sem aviso do compilador.
    /// </summary>
    [Compare("NewPassword")]
    public string NewPasswordConfirmation { get; set; } = null!;

    /// <summary>
    /// Quem está alterando, para a auditoria. Vem do cliente — mesma observação de
    /// <see cref="CreateUserRequest.CreatedBy"/>.
    /// </summary>
    [Required]
    public string ModifiedBy { get; set; } = null!;
}
