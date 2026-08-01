using Orchestrator.UseCases.Security.Authorization;
using Xunit;

namespace Orchestrator.Test.Security;

public class RoleHierarchyTests
{
    // ---------------------------------------------------------------
    // TryGetLevel — known roles
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("jogador", RoleLevel.Player)]
    [InlineData("jogador principal", RoleLevel.MainPlayer)]
    [InlineData("lider de time", RoleLevel.TeamLeader)]
    [InlineData("adm", RoleLevel.Admin)]
    [InlineData("super adm", RoleLevel.SuperAdmin)]
    public void TryGetLevel_KnownRole_ReturnsTrueWithCorrectLevel(string role, RoleLevel expected)
    {
        // Act
        var found = RoleHierarchy.TryGetLevel(role, out var level);

        // Assert
        Assert.True(found);
        Assert.Equal(expected, level);
    }

    [Theory]
    [InlineData("JOGADOR", RoleLevel.Player)]
    [InlineData("JOGADOR PRINCIPAL", RoleLevel.MainPlayer)]
    [InlineData("LIDER DE TIME", RoleLevel.TeamLeader)]
    [InlineData("ADM", RoleLevel.Admin)]
    [InlineData("SUPER ADM", RoleLevel.SuperAdmin)]
    public void TryGetLevel_UpperCaseRole_ReturnsTrueWithCorrectLevel(string role, RoleLevel expected)
    {
        // Act
        var found = RoleHierarchy.TryGetLevel(role, out var level);

        // Assert
        Assert.True(found);
        Assert.Equal(expected, level);
    }

    [Theory]
    [InlineData(" jogador ", RoleLevel.Player)]
    [InlineData(" adm ", RoleLevel.Admin)]
    [InlineData(" super adm ", RoleLevel.SuperAdmin)]
    public void TryGetLevel_RoleWithWhitespace_ReturnsTrueAfterTrimming(string role, RoleLevel expected)
    {
        // Act
        var found = RoleHierarchy.TryGetLevel(role, out var level);

        // Assert
        Assert.True(found);
        Assert.Equal(expected, level);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("admin")]
    [InlineData("player")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetLevel_InvalidRole_ReturnsFalse(string role)
    {
        // Act
        var found = RoleHierarchy.TryGetLevel(role, out _);

        // Assert
        Assert.False(found);
    }

    // ---------------------------------------------------------------
    // NormalizeRole
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(RoleLevel.Player, "jogador")]
    [InlineData(RoleLevel.MainPlayer, "jogador principal")]
    [InlineData(RoleLevel.TeamLeader, "lider de time")]
    [InlineData(RoleLevel.Admin, "adm")]
    [InlineData(RoleLevel.SuperAdmin, "super adm")]
    public void NormalizeRole_AllLevels_ReturnsExpectedString(RoleLevel level, string expected)
    {
        // Act
        var result = RoleHierarchy.NormalizeRole(level);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(RoleLevel.Player)]
    [InlineData(RoleLevel.MainPlayer)]
    [InlineData(RoleLevel.TeamLeader)]
    [InlineData(RoleLevel.Admin)]
    [InlineData(RoleLevel.SuperAdmin)]
    public void NormalizeRole_ThenTryGetLevel_RoundTripsToOriginalLevel(RoleLevel level)
    {
        // Arrange
        var normalized = RoleHierarchy.NormalizeRole(level);

        // Act
        var found = RoleHierarchy.TryGetLevel(normalized, out var roundTripped);

        // Assert
        Assert.True(found);
        Assert.Equal(level, roundTripped);
    }

    // ---------------------------------------------------------------
    // RoleLevel enum values
    // ---------------------------------------------------------------

    [Fact]
    public void RoleLevel_PlayerIsLowestLevel()
    {
        Assert.Equal(1, (int)RoleLevel.Player);
    }

    [Fact]
    public void RoleLevel_SuperAdminIsHighestLevel()
    {
        Assert.Equal(5, (int)RoleLevel.SuperAdmin);
    }

    [Fact]
    public void RoleLevel_OrderIsAscending()
    {
        Assert.True(RoleLevel.Player < RoleLevel.MainPlayer);
        Assert.True(RoleLevel.MainPlayer < RoleLevel.TeamLeader);
        Assert.True(RoleLevel.TeamLeader < RoleLevel.Admin);
        Assert.True(RoleLevel.Admin < RoleLevel.SuperAdmin);
    }
}
