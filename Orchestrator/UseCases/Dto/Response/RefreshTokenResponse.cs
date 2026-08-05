namespace Orchestrator.UseCases.Dto.Response;

/// <summary>
/// Resposta de <c>POST /refresh-token</c>: o par de credenciais renovado.
/// </summary>
public class RefreshTokenResponse
{
    /// <summary>Renovou? Em <c>false</c>, os tokens vêm <c>null</c> e a sessão acabou.</summary>
    public bool Success { get; set; }

    /// <summary>O access token novo.</summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// <b>Um refresh token novo</b>, e não o mesmo que foi enviado.
    ///
    /// <para>
    /// O cliente <b>tem de substituir</b> o que guardava por este. Continuar usando o anterior falha
    /// com "Invalid refresh token", porque a rotação já o revogou — ver
    /// <c>RefreshTokenUseCase</c>. Esquecer de guardar aqui é a causa mais provável de "a sessão cai
    /// depois de um tempo".
    /// </para>
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>Expiração do novo access token, em UTC.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// <c>"Token refreshed"</c> no sucesso. Nas falhas: <c>"Invalid user"</c> (id não é um Guid),
    /// <c>"User not found"</c>, <c>"Invalid refresh token"</c> (não existe, ou já foi revogado — os
    /// dois casos recebem a mesma mensagem de propósito) e <c>"Refresh failed"</c> para falha do
    /// sistema.
    /// </summary>
    public string Message { get; set; } = null!;
}
