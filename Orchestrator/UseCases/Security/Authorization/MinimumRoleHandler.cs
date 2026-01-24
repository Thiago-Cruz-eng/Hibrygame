using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Orchestrator.UseCases.Security.Authorization;

public class MinimumRoleHandler : AuthorizationHandler<MinimumRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MinimumRoleRequirement requirement)
    {
        var roleClaims = context.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value);
        var highestRole = RoleLevel.Player;
        var hasRole = false;

        foreach (var role in roleClaims)
        {
            if (RoleHierarchy.TryGetLevel(role, out var level))
            {
                hasRole = true;
                if (level > highestRole)
                {
                    highestRole = level;
                }
            }
        }

        if (hasRole && highestRole >= requirement.MinimumRole)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
