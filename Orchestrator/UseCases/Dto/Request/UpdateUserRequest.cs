using System.ComponentModel.DataAnnotations;
using Orchestrator.UseCases.Dto;

namespace Orchestrator.UseCases.Dto.Request;

public class UpdateUserRequest
{
    [Required]
    public string Name { get; set; } = null!;

    [Required, EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string Role { get; set; } = null!;

    public List<UserAssignmentDto> Assignments { get; set; } = new();

    [Required]
    public string ModifiedBy { get; set; } = null!;
}
