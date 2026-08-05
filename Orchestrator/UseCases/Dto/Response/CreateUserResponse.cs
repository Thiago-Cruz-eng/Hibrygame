namespace Orchestrator.UseCases.Dto.Response;

/// <summary>
/// Resposta de <c>POST /users</c>.
///
/// <para>
/// Segue a forma padrão dos responses do projeto: <see cref="Success"/> diz o que aconteceu,
/// <see cref="Message"/> explica, e os campos de dado só vêm preenchidos no sucesso. O controller
/// traduz <see cref="Success"/> em status HTTP e não faz mais nada.
/// </para>
///
/// <para>
/// Não devolve token: cadastro administrativo não abre sessão. Quem faz as duas coisas é
/// <c>POST /register</c>, com <c>RegisterResponse</c>.
/// </para>
/// </summary>
public class CreateUserResponse
{
    /// <summary>Criou?</summary>
    public bool Success { get; set; }

    /// <summary>
    /// Mensagem legível: <c>"User created"</c>, <c>"User already has a account"</c>,
    /// <c>"Invalid role"</c>, ou <c>"Same error happen"</c> para falha inesperada — nesse último
    /// caso a causa real está no log, não aqui.
    /// </summary>
    public string Message { get; set; } = null!;

    /// <summary>
    /// Id do usuário criado, como texto. <c>null</c> em caso de falha.
    ///
    /// <para>
    /// Vem preenchido sem segunda consulta ao banco, porque o id é gerado na construção da entidade
    /// e não pelo MongoDB.
    /// </para>
    /// </summary>
    public string? UserId { get; set; }
}
