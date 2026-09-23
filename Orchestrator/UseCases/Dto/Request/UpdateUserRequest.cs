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
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// E-mail novo. O caso de uso recusa se já pertencer a <b>outro</b> usuário; reenviar o próprio
    /// e-mail atual é aceito.
    /// </summary>
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = null!;

    /// <summary>
    /// Papel novo, entre os cinco válidos.
    ///
    /// <para>
    /// <b>Limitado pela alçada de quem pede.</b> <c>UpdateUserUseCase</c> recebe o nível do
    /// chamador e recusa papel acima dele — um <c>lider de time</c> não promove ninguém a
    /// <c>adm</c>. Era por aqui que alguém subia de nível sozinho (DT-16).
    /// </para>
    /// </summary>
    [Required, StringLength(32)]
    public string Role { get; set; } = null!;

    /// <summary>
    /// Lista completa de vínculos após a alteração. Substitui a atual inteira — ver a nota da
    /// classe.
    /// </summary>
    public List<UserAssignmentDto> Assignments { get; set; } = new();

    /// <summary>
    /// <b>Ignorado.</b> A auditoria grava o claim <c>sub</c> de quem chamou, nunca o que vem no
    /// corpo. Mantido no DTO, sem <c>[Required]</c>, só para não quebrar o cliente atual.
    /// </summary>
    [StringLength(100)]
    public string ModifiedBy { get; set; } = null!;
}
