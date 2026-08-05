using System.ComponentModel.DataAnnotations;

namespace Orchestrator.UseCases.Dto.Request;

/// <summary>
/// Corpo de <c>PUT /users/{id}</c>.
///
/// <para>
/// <b>É substituição, não alteração parcial.</b> Todos os campos são aplicados ao usuário, então
/// omitir <see cref="Assignments"/> — ou enviá-lo vazio — <b>apaga</b> os vínculos existentes.
/// Para alterar só um campo, envie os outros com os valores atuais.
/// </para>
///
/// <para>
/// Note que a senha não está aqui: trocar senha é operação própria, em
/// <see cref="ChangePasswordRequest"/>, porque exige conferir a senha atual.
/// </para>
/// </summary>
public class UpdateUserRequest
{
    /// <summary>Nome novo. Obrigatório mesmo quando não está mudando.</summary>
    [Required]
    public string Name { get; set; } = null!;

    /// <summary>
    /// E-mail novo. O caso de uso recusa se já pertencer a <b>outro</b> usuário; reenviar o próprio
    /// e-mail atual é aceito.
    /// </summary>
    [Required, EmailAddress]
    public string Email { get; set; } = null!;

    /// <summary>
    /// Papel novo, entre os cinco válidos.
    ///
    /// <para>
    /// <b>Nada confere a alçada de quem está pedindo</b> — é por aqui que um usuário pode se
    /// promover, se alcançar o endpoint (DT-16).
    /// </para>
    /// </summary>
    [Required]
    public string Role { get; set; } = null!;

    /// <summary>
    /// Lista completa de vínculos após a alteração. Substitui a atual inteira — ver a nota da
    /// classe.
    /// </summary>
    public List<UserAssignmentDto> Assignments { get; set; } = new();

    /// <summary>
    /// Quem está alterando, para a auditoria. <b>Vem do cliente</b> e não do token — mesma
    /// observação de <see cref="CreateUserRequest.CreatedBy"/>.
    /// </summary>
    [Required]
    public string ModifiedBy { get; set; } = null!;
}
