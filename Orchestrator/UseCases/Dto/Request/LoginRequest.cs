using System.ComponentModel.DataAnnotations;

namespace Orchestrator.UseCases.Dto.Request;

/// <summary>
/// Corpo de <c>POST /login</c>.
///
/// <para>
/// O endpoint é anônimo, por necessidade — é ele que produz a credencial. A resposta
/// (<c>LoginResponse</c>) não distingue e-mail inexistente de senha errada, de propósito.
/// </para>
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// E-mail cadastrado. Maiúsculas e espaços nas pontas são tolerados: o caso de uso normaliza
    /// antes de procurar, com a mesma regra usada no cadastro.
    /// </summary>
    [Required, EmailAddress]
    public string Email { get; set; } = null!;

    /// <summary>Senha em claro, conferida contra o hash gravado.</summary>
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = null!;
}
