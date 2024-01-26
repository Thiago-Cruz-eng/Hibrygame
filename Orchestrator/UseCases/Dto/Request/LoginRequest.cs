using System.ComponentModel.DataAnnotations;

namespace Orchestrator.UseCases.Dto.Request;

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; }
    [Required, DataType(DataType.Password)]
    public string Password { get; set; }
}