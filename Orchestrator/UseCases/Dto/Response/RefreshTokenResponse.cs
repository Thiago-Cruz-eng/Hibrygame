namespace Orchestrator.UseCases.Dto.Response;

public class RefreshTokenResponse
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string Message { get; set; } = null!;
}
