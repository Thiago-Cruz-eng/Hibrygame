using AspNetCore.Identity.MongoDbCore.Models;
using MongoDbGenericRepository.Attributes;

namespace Orchestrator.Domain;

[CollectionName("roles")]
public class Roles : MongoIdentityRole<Guid>
{
}