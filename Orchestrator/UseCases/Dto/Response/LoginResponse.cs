namespace Orchestrator.UseCases.Dto.Response;

public class LoginResponse
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Email { get; set; }
    public string? UserId { get; set; }
    public string? Role { get; set; }
    public bool MustChangePassword { get; set; }
    public string Message { get; set; } = null!;
}
