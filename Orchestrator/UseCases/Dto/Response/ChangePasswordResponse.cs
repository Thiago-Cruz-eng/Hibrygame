namespace Orchestrator.UseCases.Dto.Response;

/// <summary>
/// Resposta de <c>POST /users/change-password</c>.
///
/// <para>
/// Não devolve tokens: trocar a senha <b>não</b> invalida as sessões abertas nem os refresh tokens
/// já emitidos. Quem tinha um refresh token válido continua conseguindo renovar depois da troca.
/// Se esse comportamento tiver de mudar, o lugar é <c>ChangePasswordUseCase</c>, revogando os
/// tokens do usuário.
/// </para>
/// </summary>
public class ChangePasswordResponse
{
    /// <summary>Trocou?</summary>
    public bool Success { get; set; }

    /// <summary>
    /// <c>"Password updated"</c> no sucesso. <c>"User not found"</c>,
    /// <c>"Invalid credentials"</c> (senha atual errada) ou <c>"Same error happen"</c> nas falhas.
    /// </summary>
    public string Message { get; set; } = null!;
}
