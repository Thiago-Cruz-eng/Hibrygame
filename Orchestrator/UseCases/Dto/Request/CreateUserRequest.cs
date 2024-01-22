using System.ComponentModel.DataAnnotations;

namespace Orchestrator.UseCases.Dto;

public class CreateUserRequest
{
    [Required]
    public string UserName { get; set; }
    [Required]
    public string Email { get; set; }
    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; }
    [Compare("Password")]
    public string PasswordConfirmation { get; set; }
    public DateTime? CreateAt { get; set; } = DateTime.Now;
    [Required]
    public DateTime DateBirth { get; set; }
    [Required]
    public string Address { get; set; }
    [Required]
    public string PhoneNumber { get; set; }
}