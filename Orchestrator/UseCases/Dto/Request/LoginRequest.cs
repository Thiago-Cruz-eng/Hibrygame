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
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = null!;

    /// <summary>
    /// Senha em claro, conferida contra o hash gravado.
    ///
    /// <para>
    /// <b>Sem mínimo aqui, de propósito</b>, ao contrário do cadastro: contas criadas antes da
    /// política de 8 caracteres têm de continuar conseguindo entrar — e depois trocar a senha. O
    /// teto de 128 existe só para não pagar PBKDF2 sobre um texto enorme.
    /// </para>
    /// </summary>
    [Required, DataType(DataType.Password), StringLength(128)]
    public string Password { get; set; } = null!;
}
