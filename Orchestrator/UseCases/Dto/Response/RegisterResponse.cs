namespace Orchestrator.UseCases.Dto.Response;

/// <summary>
/// Resposta do auto-registro: mesma forma de <see cref="LoginResponse"/> mais o nome.
///
/// Traz sessao pronta de proposito. O frontend navega para o lobby direto apos o
/// cadastro, e antes fazia isso sem token nenhum em maos — porque
/// <see cref="CreateUserResponse"/> nao devolve token —, caindo em "Sessao invalida" na
/// tela seguinte.
/// </summary>
public class RegisterResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
    public string? UserId { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
