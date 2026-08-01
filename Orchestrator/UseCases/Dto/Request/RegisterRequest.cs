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
    [Required]
    public string Name { get; set; } = null!;

    [Required, EmailAddress]
    public string Email { get; set; } = null!;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = null!;

    [Required, Compare("Password")]
    public string PasswordConfirmation { get; set; } = null!;
}
