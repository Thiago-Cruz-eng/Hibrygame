namespace Orchestrator.UseCases.Dto.Response;

/// <summary>
/// Resposta de <c>GET /users/{id}</c>.
///
/// <para>
/// <b>Não tem <c>Success</c> nem <c>Message</c></b>, diferente dos outros responses: é leitura, e
/// "não encontrei" é comunicado por 404, não por um campo. Quando o corpo existe, ele é o usuário.
/// </para>
///
/// <para>
/// <b>Os campos que NÃO estão aqui são o ponto deste tipo:</b> <c>PasswordHash</c> e <c>Salt</c>
/// existem na entidade e ficam de fora de propósito. É por isso que <c>GetUserUseCase</c> monta a
/// resposta campo a campo em vez de devolver a entidade — trocar isso por "devolver o usuário"
/// exporia os dois numa resposta HTTP.
/// </para>
/// </summary>
public class GetUserResponse
{
    /// <summary>Id do usuário, como texto.</summary>
    public string Id { get; set; } = null!;

    /// <summary>Nome de exibição.</summary>
    public string Name { get; set; } = null!;

    /// <summary>E-mail, na forma normalizada em que está gravado.</summary>
    public string Email { get; set; } = null!;

    /// <summary>Papel de autorização, em português.</summary>
    public string Role { get; set; } = null!;

    /// <summary>Se a senha precisa ser trocada no próximo acesso.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Vínculos do usuário. Lista vazia quando não há nenhum — nunca <c>null</c>.</summary>
    public List<UserAssignmentDto> Assignments { get; set; } = new();
}
