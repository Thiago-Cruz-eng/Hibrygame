using Microsoft.AspNetCore.Authorization;

namespace Orchestrator.UseCases.Security.Authorization;

public class MinimumRoleRequirement : IAuthorizationRequirement
{
    public MinimumRoleRequirement(RoleLevel minimumRole)
    {
        MinimumRole = minimumRole;
    }

    public RoleLevel MinimumRole { get; }
}
