using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Orchestrator.UseCases.Security.Authorization;
using Xunit;

namespace Orchestrator.Test.Security;

public class MinimumRoleHandlerTests
{
    private readonly MinimumRoleHandler _sut = new();

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static AuthorizationHandlerContext BuildContext(
        MinimumRoleRequirement requirement,
        params string[] roles)
    {
        var claims = roles
            .Select(r => new Claim(ClaimTypes.Role, r))
            .ToList<Claim>();

        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);

        return new AuthorizationHandlerContext(
            new[] { requirement },
            principal,
            resource: null);
    }

    // ---------------------------------------------------------------
    // User role meets requirement
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("jogador", RoleLevel.Player)]
    [InlineData("jogador principal", RoleLevel.MainPlayer)]
    [InlineData("lider de time", RoleLevel.TeamLeader)]
    [InlineData("adm", RoleLevel.Admin)]
    [InlineData("super adm", RoleLevel.SuperAdmin)]
    public async Task HandleRequirementAsync_UserRoleEqualsRequirement_Succeeds(string roleString, RoleLevel requiredLevel)
    {
        // Arrange
        var requirement = new MinimumRoleRequirement(requiredLevel);
        var context = BuildContext(requirement, roleString);

        // Act
        await _sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [InlineData("jogador principal", RoleLevel.Player)]
    [InlineData("lider de time", RoleLevel.MainPlayer)]
    [InlineData("adm", RoleLevel.TeamLeader)]
    [InlineData("super adm", RoleLevel.Admin)]
    [InlineData("super adm", RoleLevel.Player)]
    public async Task HandleRequirementAsync_UserRoleExceedsRequirement_Succeeds(string roleString, RoleLevel requiredLevel)
    {
        // Arrange
        var requirement = new MinimumRoleRequirement(requiredLevel);
        var context = BuildContext(requirement, roleString);

        // Act
        await _sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    // ---------------------------------------------------------------
    // User role does not meet requirement
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("jogador", RoleLevel.MainPlayer)]
    [InlineData("jogador", RoleLevel.TeamLeader)]
    [InlineData("jogador principal", RoleLevel.Admin)]
    [InlineData("lider de time", RoleLevel.SuperAdmin)]
    public async Task HandleRequirementAsync_UserRoleBelowRequirement_DoesNotSucceed(string roleString, RoleLevel requiredLevel)
    {
        // Arrange
        var requirement = new MinimumRoleRequirement(requiredLevel);
        var context = BuildContext(requirement, roleString);

        // Act
        await _sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    // ---------------------------------------------------------------
    // No role claims
    // ---------------------------------------------------------------

    [Fact]
    public async Task HandleRequirementAsync_UserHasNoRoleClaims_DoesNotSucceed()
    {
        // Arrange
        var requirement = new MinimumRoleRequirement(RoleLevel.Player);
        var context = BuildContext(requirement); // no roles

        // Act
        await _sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_UserHasUnrecognizedRoleOnly_DoesNotSucceed()
    {
        // Arrange
        var requirement = new MinimumRoleRequirement(RoleLevel.Player);
        var context = BuildContext(requirement, "unknown-role");

        // Act
        await _sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    // ---------------------------------------------------------------
    // Multiple role claims — uses highest
    // ---------------------------------------------------------------

    [Fact]
    public async Task HandleRequirementAsync_UserHasMultipleRoles_UsesHighestLevel()
    {
        // Arrange — user has Player and Admin; requirement is TeamLeader
        var requirement = new MinimumRoleRequirement(RoleLevel.TeamLeader);
        var context = BuildContext(requirement, "jogador", "adm");

        // Act
        await _sut.HandleAsync(context);

        // Assert — Admin > TeamLeader, so should succeed
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_UserHasMultipleRolesAllBelowRequirement_DoesNotSucceed()
    {
        // Arrange — user has Player and MainPlayer; requirement is Admin
        var requirement = new MinimumRoleRequirement(RoleLevel.Admin);
        var context = BuildContext(requirement, "jogador", "jogador principal");

        // Act
        await _sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_UserHasMixedValidAndInvalidRoles_UsesHighestValidLevel()
    {
        // Arrange — user has recognized "lider de time" and an unrecognized "unknown"; requirement is TeamLeader
        var requirement = new MinimumRoleRequirement(RoleLevel.TeamLeader);
        var context = BuildContext(requirement, "lider de time", "unknown-role");

        // Act
        await _sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    // ---------------------------------------------------------------
    // All five role levels explicitly verified
    // ---------------------------------------------------------------

    [Fact]
    public async Task HandleRequirementAsync_PlayerRequiredAndPlayerProvided_Succeeds()
    {
        // Arrange
        var requirement = new MinimumRoleRequirement(RoleLevel.Player);
        var context = BuildContext(requirement, "jogador");

        // Act
        await _sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_SuperAdminRequiredAndSuperAdminProvided_Succeeds()
    {
        // Arrange
        var requirement = new MinimumRoleRequirement(RoleLevel.SuperAdmin);
        var context = BuildContext(requirement, "super adm");

        // Act
        await _sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_SuperAdminRequiredAndAdminProvided_DoesNotSucceed()
    {
        // Arrange
        var requirement = new MinimumRoleRequirement(RoleLevel.SuperAdmin);
        var context = BuildContext(requirement, "adm");

        // Act
        await _sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }
}
