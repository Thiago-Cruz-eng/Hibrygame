namespace Orchestrator.UseCases.Dto.Response;

/// <summary>
/// Resposta de <c>DELETE /users/{id}</c>.
/// </summary>
public class DeleteUserResponse
{
    /// <summary>Removeu? A remoção é física e não há como desfazer.</summary>
    public bool Success { get; set; }

    /// <summary>
    /// <c>"User deleted"</c> no sucesso. <c>"User not found"</c> quando não existe, e
    /// <c>"User not deleted"</c> quando existia na consulta mas a remoção não afetou documento
    /// nenhum — na prática, alguém o removeu no intervalo.
    /// </summary>
    public string Message { get; set; } = null!;
}
