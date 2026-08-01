using System.ComponentModel.DataAnnotations;

namespace Orchestrator.UseCases.Dto.Request;

public class VerifyValidationRequest
{
    [Required]
    public string UserId { get; set; } = null!;
}

public class GetValidationRequest
{
    [Required]
    public string UserId { get; set; } = null!;
}

public class UpdateValidationRequest
{
    [Required]
    public string UserId { get; set; } = null!;

    [Required]
    public string Room { get; set; } = null!;

    [Required]
    public string PieceColor { get; set; } = null!;

    [Required, EmailAddress]
    public string UserEmail { get; set; } = null!;
}

public class CanMoveValidationRequest
{
    [Required]
    public string UserId { get; set; } = null!;

    [Required]
    public string Room { get; set; } = null!;

    [Required]
    public string PieceColor { get; set; } = null!;

    [Required, EmailAddress]
    public string UserEmail { get; set; } = null!;

    [Required]
    public string Day { get; set; } = null!;
}
