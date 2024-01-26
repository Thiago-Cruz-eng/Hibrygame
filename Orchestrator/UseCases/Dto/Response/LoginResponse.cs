namespace Orchestrator.UseCases.Dto.Response;

public class LoginResponse
{
    public bool Success { get; set; }
    public string AccessToken { get; set; }
    public string Email { get; set; }
    public string UserId { get; set; }
    public string Message { get; set; }
}