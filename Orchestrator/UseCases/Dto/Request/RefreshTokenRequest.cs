using System.ComponentModel.DataAnnotations;

namespace Orchestrator.UseCases.Dto.Request;

/// <summary>
/// Corpo de <c>POST /refresh-token</c>: troca um refresh token válido por credenciais novas.
///
/// <para>
/// A chamada <b>gasta</b> o token enviado. A resposta traz um refresh token novo, e é ele que o
/// cliente tem de guardar — reenviar o antigo falha. Ver <c>RefreshTokenUseCase</c>.
/// </para>
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// Dono do token, como texto. Precisa ser um <see cref="Guid"/> válido; qualquer outra coisa
    /// devolve "Invalid user".
    ///
    /// <para>
    /// Vai no corpo, e não é lido do access token, porque este endpoint tem de funcionar
    /// justamente quando o access token já expirou.
    /// </para>
    /// </summary>
    [Required]
    public string UserId { get; set; } = null!;

    /// <summary>
    /// O refresh token em claro, como foi recebido no login ou no refresh anterior. No banco existe
    /// só o hash dele, então a conferência é feita re-derivando o hash — ver
    /// <c>RefreshTokenUseCase</c>.
    /// </summary>
    [Required]
    public string RefreshToken { get; set; } = null!;
}
