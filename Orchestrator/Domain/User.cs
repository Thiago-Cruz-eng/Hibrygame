using AspNetCore.Identity.MongoDbCore.Models;
using Microsoft.AspNet.Identity;
using MongoDbGenericRepository.Attributes;

namespace Orchestrator.Domain;

[CollectionName("users")]
public class User : MongoIdentityUser<Guid>
{
    public string Password { get; set; }
    public string PasswordConfirmation { get; set; }
    public DateTime? CreateAt { get; set; } = DateTime.Now;
    public DateTime DateBirth { get; set; }
    public string Address { get; set; }
}