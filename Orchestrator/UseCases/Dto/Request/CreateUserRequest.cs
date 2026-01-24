using System.ComponentModel.DataAnnotations;
using Orchestrator.UseCases.Dto;

namespace Orchestrator.UseCases.Dto.Request;

public class CreateUserRequest
{
    [Required]
    public string Name { get; set; } = null!;

    [Required, EmailAddress]
    public string Email { get; set; } = null!;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = null!;

    [Required]
    public string Role { get; set; } = null!;

    [Compare("Password")]
    public string PasswordConfirmation { get; set; } = null!;

    public List<UserAssignmentDto> Assignments { get; set; } = new();

    [Required]
    public string CreatedBy { get; set; } = null!;
}
