﻿using System.ComponentModel.DataAnnotations;

namespace Orchestrator.UseCases.Dto.Request;

public class CreateUserRequest
{
    [Required]
    public string UserName { get; set; }
    [Required, EmailAddress]
    public string Email { get; set; }
    [Required, DataType(DataType.Password)]
    public string Password { get; set; }
    [Compare("Password")]
    public string PasswordConfirmation { get; set; }
    public DateTime? CreateAt { get; set; } = DateTime.Now;
    public DateTime? DateBirth { get; set; }
    public string PhoneNumber { get; set; }
}