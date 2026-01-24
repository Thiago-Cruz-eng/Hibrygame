using System.ComponentModel.DataAnnotations;

namespace Orchestrator.UseCases.Dto.Request;

public class ChangePasswordRequest
{
    [Required]
    public string UserId { get; set; } = null!;

    [Required, DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = null!;

    [Required, DataType(DataType.Password)]
    public string NewPassword { get; set; } = null!;

    [Compare("NewPassword")]
    public string NewPasswordConfirmation { get; set; } = null!;

    [Required]
    public string ModifiedBy { get; set; } = null!;
}
