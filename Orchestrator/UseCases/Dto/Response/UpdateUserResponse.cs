namespace Orchestrator.UseCases.Dto.Response;

/// <summary>
/// Resposta de <c>PUT /users/{id}</c>. Só diz se deu certo — a alteração não devolve o usuário.
///
/// <para>
/// Três tipos deste projeto (<see cref="UpdateUserResponse"/>, <see cref="DeleteUserResponse"/> e
/// <see cref="ChangePasswordResponse"/>) têm exatamente esta forma. São mantidos separados de
/// propósito: um tipo por ação é o que permite acrescentar campo a uma resposta sem alterar o
/// contrato das outras.
/// </para>
/// </summary>
public class UpdateUserResponse
{
    /// <summary>Alterou?</summary>
    public bool Success { get; set; }

    /// <summary>
    /// <c>"User updated"</c> no sucesso. Nas falhas: <c>"User not found"</c>,
    /// <c>"Email already in use"</c>, <c>"Invalid role"</c>, ou <c>"Same error happen"</c> para falha
    /// inesperada.
    /// </summary>
    public string Message { get; set; } = null!;
}
