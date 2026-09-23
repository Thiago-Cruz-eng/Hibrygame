using Microsoft.Extensions.Logging;
using Moq;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Dto;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Security.Authorization;
using Xunit;

namespace Orchestrator.Test.UseCases;

public class UpdateUserUseCaseTests
{
    private readonly Mock<IUserRepositoryNoSql> _userRepositoryMock;
    private readonly Mock<ILogger<UpdateUserUseCase>> _loggerMock;
    private readonly UpdateUserUseCase _sut;

    public UpdateUserUseCaseTests()
    {
        _userRepositoryMock = new Mock<IUserRepositoryNoSql>();
        _loggerMock = new Mock<ILogger<UpdateUserUseCase>>();
        _sut = new UpdateUserUseCase(_userRepositoryMock.Object, _loggerMock.Object);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Alçada do chamador nos testes que não estão exercitando a alçada. <c>super adm</c> é o
    /// nível que passa por qualquer comparação, deixando cada teste isolar o que ele mede.
    /// </summary>
    private const RoleLevel CallerLevel = RoleLevel.SuperAdmin;

    /// <summary>Id do chamador — é ele que vai para a auditoria, e não o ModifiedBy do corpo.</summary>
    private const string CallerId = "caller-do-token";

    private static User BuildUser(string name = "Test User", string email = "test@example.com", string role = "jogador")
        => User.Create(name, email, role, "hash", "salt", new List<UserAssignment>(), "admin");

    private static UpdateUserRequest BuildRequest(
        string name = "Updated User",
        string email = "updated@example.com",
        string role = "jogador",
        string modifiedBy = "admin")
        => new()
        {
            Name = name,
            Email = email,
            Role = role,
            ModifiedBy = modifiedBy,
            Assignments = new List<UserAssignmentDto>()
        };

    /// <summary>
    /// Sets up FindByFilter so the first call returns the target user and
    /// subsequent calls (email collision check) return an empty list.
    /// </summary>
    private void SetupUserFoundNoCollision(User user)
    {
        var callCount = 0;
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? new[] { user } : Enumerable.Empty<User>();
            });
    }

    private void SetupUserNotFound()
    {
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<User>());
    }

    // ---------------------------------------------------------------
    // Happy path
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ValidRequest_ReturnsSuccessTrue()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFoundNoCollision(user);
        _userRepositoryMock
            .Setup(r => r.Update(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.UpdateAsync(user.Id.ToString(), BuildRequest(), CallerLevel, CallerId);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_ReturnsMessageUserUpdated()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFoundNoCollision(user);
        _userRepositoryMock
            .Setup(r => r.Update(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.UpdateAsync(user.Id.ToString(), BuildRequest(), CallerLevel, CallerId);

        // Assert
        Assert.Equal("User updated", result.Message);
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_CallsUpdateOnRepository()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFoundNoCollision(user);
        _userRepositoryMock
            .Setup(r => r.Update(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.UpdateAsync(user.Id.ToString(), BuildRequest(), CallerLevel, CallerId);

        // Assert
        _userRepositoryMock.Verify(r => r.Update(user.Id.ToString(), It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------
    // User not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_UserNotFound_ReturnsSuccessFalse()
    {
        // Arrange
        SetupUserNotFound();

        // Act
        var result = await _sut.UpdateAsync(Guid.NewGuid().ToString(), BuildRequest(), CallerLevel, CallerId);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task UpdateAsync_UserNotFound_ReturnsMessageUserNotFound()
    {
        // Arrange
        SetupUserNotFound();

        // Act
        var result = await _sut.UpdateAsync(Guid.NewGuid().ToString(), BuildRequest(), CallerLevel, CallerId);

        // Assert
        Assert.Equal("User not found", result.Message);
    }

    // ---------------------------------------------------------------
    // Email collision
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_EmailInUseByAnotherUser_ReturnsSuccessFalse()
    {
        // Arrange
        var user = BuildUser();
        var anotherUser = BuildUser(email: "other@example.com");

        var callCount = 0;
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // First call: find the user being updated
                // Second call: find collision → returns another user
                return callCount == 1
                    ? new[] { user }
                    : new[] { anotherUser };
            });

        // Act
        var result = await _sut.UpdateAsync(user.Id.ToString(), BuildRequest(email: "other@example.com"), CallerLevel, CallerId);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task UpdateAsync_EmailInUseByAnotherUser_ReturnsMessageEmailAlreadyInUse()
    {
        // Arrange
        var user = BuildUser();
        var anotherUser = BuildUser(email: "other@example.com");

        var callCount = 0;
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? new[] { user }
                    : new[] { anotherUser };
            });

        // Act
        var result = await _sut.UpdateAsync(user.Id.ToString(), BuildRequest(email: "other@example.com"), CallerLevel, CallerId);

        // Assert
        Assert.Equal("Email already in use", result.Message);
    }

    // ---------------------------------------------------------------
    // Invalid role
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("not-a-role")]
    [InlineData("")]
    [InlineData("SUPER ADMIN")]
    public async Task UpdateAsync_InvalidRole_ReturnsSuccessFalse(string invalidRole)
    {
        // Arrange
        var user = BuildUser();
        SetupUserFoundNoCollision(user);

        // Act
        var result = await _sut.UpdateAsync(user.Id.ToString(), BuildRequest(role: invalidRole), CallerLevel, CallerId);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task UpdateAsync_InvalidRole_ReturnsMessageInvalidRole()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFoundNoCollision(user);

        // Act
        var result = await _sut.UpdateAsync(user.Id.ToString(), BuildRequest(role: "bad-role"), CallerLevel, CallerId);

        // Assert
        Assert.Equal("Invalid role", result.Message);
    }

    // ---------------------------------------------------------------
    // Alcada: quem se altera e o que se concede
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_TargetOutranksTheCaller_IsRejected()
    {
        // Um "lider de time" (nivel 3) nao edita um "adm" (nivel 4): podia rebaixa-lo, trocar o
        // e-mail dele e assumir a conta. Era a DT-16.
        var user = BuildUser(role: "adm");
        SetupUserFoundNoCollision(user);

        var result = await _sut.UpdateAsync(
            user.Id.ToString(), BuildRequest(), RoleLevel.TeamLeader, CallerId);

        Assert.False(result.Success);
        Assert.Equal("Cannot modify a user with a role above your own.", result.Message);
        _userRepositoryMock.Verify(
            r => r.Update(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_TargetAtTheSameLevel_IsAllowed()
    {
        var user = BuildUser(role: "lider de time");
        SetupUserFoundNoCollision(user);
        _userRepositoryMock
            .Setup(r => r.Update(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.UpdateAsync(
            user.Id.ToString(), BuildRequest(role: "lider de time"), RoleLevel.TeamLeader, CallerId);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task UpdateAsync_RequestedRoleAboveTheCallers_IsRejected()
    {
        // Era por aqui que um "lider de time" se promovia a "super adm".
        var user = BuildUser();
        SetupUserFoundNoCollision(user);

        var result = await _sut.UpdateAsync(
            user.Id.ToString(), BuildRequest(role: "super adm"), RoleLevel.TeamLeader, CallerId);

        Assert.False(result.Success);
        Assert.Equal("Cannot assign a role above your own.", result.Message);
        _userRepositoryMock.Verify(
            r => r.Update(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_UnknownRoleIsReportedAsInvalid_NotAsAboveYourLevel()
    {
        // A ordem das duas checagens importa: validar o papel ANTES de comparar niveis. Trocada,
        // um papel inexistente viraria "acima do seu nivel" e mandaria quem depura para o lugar
        // errado.
        var user = BuildUser();
        SetupUserFoundNoCollision(user);

        var result = await _sut.UpdateAsync(
            user.Id.ToString(), BuildRequest(role: "imperador"), RoleLevel.Player, CallerId);

        Assert.Equal("Invalid role", result.Message);
    }

    [Fact]
    public async Task UpdateAsync_AuditsTheCallerFromTheToken_NotTheBody()
    {
        var user = BuildUser();
        SetupUserFoundNoCollision(user);
        _userRepositoryMock
            .Setup(r => r.Update(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.UpdateAsync(
            user.Id.ToString(), BuildRequest(modifiedBy: "forjado-pelo-cliente"), CallerLevel, CallerId);

        Assert.NotNull(user.ModificationInformations);
        Assert.Equal(CallerId, user.ModificationInformations!.ModifiedBy);
        Assert.NotEqual("forjado-pelo-cliente", user.ModificationInformations.ModifiedBy);
    }

    // ---------------------------------------------------------------
    // Exception
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_RepositoryThrows_ReturnsSuccessFalse()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        // Act
        var result = await _sut.UpdateAsync(Guid.NewGuid().ToString(), BuildRequest(), CallerLevel, CallerId);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task UpdateAsync_RepositoryThrows_DoesNotPropagateException()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        // Act
        var exception = await Record.ExceptionAsync(() =>
            _sut.UpdateAsync(Guid.NewGuid().ToString(), BuildRequest(), CallerLevel, CallerId));

        // Assert
        Assert.Null(exception);
    }
}
