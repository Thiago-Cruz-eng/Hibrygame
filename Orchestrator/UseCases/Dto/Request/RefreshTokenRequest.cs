using System.ComponentModel.DataAnnotations;

namespace Orchestrator.UseCases.Dto.Request;

public class RefreshTokenRequest
{
    [Required]
    public string UserId { get; set; } = null!;

    [Required]
    public string RefreshToken { get; set; } = null!;
}
