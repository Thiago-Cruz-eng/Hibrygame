using Microsoft.AspNetCore.Identity;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Orchestrator.Domain;

public class User : IdentityUser
{
    public string Password { get; set; }
    public string PasswordConfirmation { get; set; }
    public DateTime? CreateAt { get; set; } = DateTime.Now;
    public DateTime DateBirth { get; set; }
    public string Address { get; set; }
}