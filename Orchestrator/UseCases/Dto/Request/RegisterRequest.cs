using System.ComponentModel.DataAnnotations;

namespace Orchestrator.UseCases.Dto.Request;

/// <summary>
/// Auto-registro. Note o que NAO esta aqui: <c>Role</c> e <c>CreatedBy</c>.
///
/// Os dois sao decididos no servidor — papel fixo em "jogador", autor fixo em
/// "self-registration". <c>CreateUserRequest</c> aceita ambos pelo corpo, e por isso o
/// endpoint que o consome exige Role:Admin. Ver DT-04 em docs/debito-tecnico.md.
/// </summary>
public class RegisterRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = null!;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = null!;

    /// <summary>
    /// Senha em claro. Mínimo de 8 caracteres, teto de 128 — mesma política de
    /// <see cref="CreateUserRequest.Password"/>, e os dois têm de andar juntos: se só o cadastro
    /// administrativo exigisse tamanho, o auto-registro seria a porta de entrada para senha fraca.
    /// </summary>
    [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = null!;

    [Required, Compare("Password")]
    public string PasswordConfirmation { get; set; } = null!;
}
