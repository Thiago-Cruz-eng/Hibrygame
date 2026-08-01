using Microsoft.Extensions.Logging;
using Moq;
using Orchestrator.Domain;
using Orchestrator.Infra.Mongo;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Interfaces;
using System.Linq.Expressions;
using Xunit;

namespace Orchestrator.Test.UseCases;

public class ChangePasswordUseCaseTests
{
    private readonly Mock<IGenericRepository> _genericRepositoryMock;
    private readonly Mock<ISecureHashingService> _hashingServiceMock;
    private readonly Mock<ILogger<ChangePasswordUseCase>> _loggerMock;
    private readonly ChangePasswordUseCase _sut;

    public ChangePasswordUseCaseTests()
    {
        _genericRepositoryMock = new Mock<IGenericRepository>();
        _hashingServiceMock = new Mock<ISecureHashingService>();
        _loggerMock = new Mock<ILogger<ChangePasswordUseCase>>();
        _sut = new ChangePasswordUseCase(
            _genericRepositoryMock.Object,
            _hashingServiceMock.Object,
            _loggerMock.Object);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static User BuildUser(string passwordHash = "old-hash", string salt = "old-salt")
        => User.Create("Test User", "test@example.com", "jogador", passwordHash, salt, new List<UserAssignment>(), "admin");

    private static ChangePasswordRequest BuildRequest(
        string userId,
        string currentPassword = "OldPass123",
        string newPassword = "NewPass456",
        string modifiedBy = "admin")
        => new()
        {
            UserId = userId,
            CurrentPassword = currentPassword,
            NewPassword = newPassword,
            NewPasswordConfirmation = newPassword,
            ModifiedBy = modifiedBy
        };

    // ---------------------------------------------------------------
    // Happy path
    // ---------------------------------------------------------------

    [Fact]
    public async Task ChangeAsync_ValidCredentials_ReturnsSuccessTrue()
    {
        // Arrange
        var user = BuildUser();
        _genericRepositoryMock
            .Setup(r => r.GetFirstOrDefault<User>(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hashingServiceMock
            .Setup(h => h.Verify("OldPass123", user.PasswordHash, user.Salt))
            .Returns(true);
        _hashingServiceMock
            .Setup(h => h.HashValue("NewPass456"))
            .Returns(("new-hash", "new-salt"));
        _genericRepositoryMock
            .Setup(r => r.Update<User>(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>(),
                It.IsAny<(Expression<Func<User, object>>, object)[]>()))
            .ReturnsAsync(true);

        var req = BuildRequest(user.Id.ToString());

        // Act
        var result = await _sut.ChangeAsync(req);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ChangeAsync_ValidCredentials_ReturnsMessagePasswordUpdated()
    {
        // Arrange
        var user = BuildUser();
        _genericRepositoryMock
            .Setup(r => r.GetFirstOrDefault<User>(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hashingServiceMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _hashingServiceMock.Setup(h => h.HashValue(It.IsAny<string>())).Returns(("new-hash", "new-salt"));
        _genericRepositoryMock
            .Setup(r => r.Update<User>(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>(),
                It.IsAny<(Expression<Func<User, object>>, object)[]>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ChangeAsync(BuildRequest(user.Id.ToString()));

        // Assert
        Assert.Equal("Password updated", result.Message);
    }

    [Fact]
    public async Task ChangeAsync_ValidCredentials_CallsUpdateOnRepository()
    {
        // Arrange
        var user = BuildUser();
        _genericRepositoryMock
            .Setup(r => r.GetFirstOrDefault<User>(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hashingServiceMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _hashingServiceMock.Setup(h => h.HashValue(It.IsAny<string>())).Returns(("new-hash", "new-salt"));
        _genericRepositoryMock
            .Setup(r => r.Update<User>(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>(),
                It.IsAny<(Expression<Func<User, object>>, object)[]>()))
            .ReturnsAsync(true);

        // Act
        await _sut.ChangeAsync(BuildRequest(user.Id.ToString()));

        // Assert
        _genericRepositoryMock.Verify(
            r => r.Update<User>(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>(),
                It.IsAny<(Expression<Func<User, object>>, object)[]>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // User not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task ChangeAsync_UserNotFound_ReturnsSuccessFalse()
    {
        // Arrange
        _genericRepositoryMock
            .Setup(r => r.GetFirstOrDefault<User>(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _sut.ChangeAsync(BuildRequest(Guid.NewGuid().ToString()));

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ChangeAsync_UserNotFound_ReturnsMessageUserNotFound()
    {
        // Arrange
        _genericRepositoryMock
            .Setup(r => r.GetFirstOrDefault<User>(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _sut.ChangeAsync(BuildRequest(Guid.NewGuid().ToString()));

        // Assert
        Assert.Equal("User not found", result.Message);
    }

    // ---------------------------------------------------------------
    // Wrong current password
    // ---------------------------------------------------------------

    [Fact]
    public async Task ChangeAsync_WrongCurrentPassword_ReturnsSuccessFalse()
    {
        // Arrange
        var user = BuildUser();
        _genericRepositoryMock
            .Setup(r => r.GetFirstOrDefault<User>(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hashingServiceMock
            .Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        // Act
        var result = await _sut.ChangeAsync(BuildRequest(user.Id.ToString(), currentPassword: "WrongPass!"));

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ChangeAsync_WrongCurrentPassword_ReturnsMessageInvalidCredentials()
    {
        // Arrange
        var user = BuildUser();
        _genericRepositoryMock
            .Setup(r => r.GetFirstOrDefault<User>(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hashingServiceMock
            .Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        // Act
        var result = await _sut.ChangeAsync(BuildRequest(user.Id.ToString(), currentPassword: "WrongPass!"));

        // Assert
        Assert.Equal("Invalid credentials", result.Message);
    }

    // ---------------------------------------------------------------
    // Exception
    // ---------------------------------------------------------------

    [Fact]
    public async Task ChangeAsync_RepositoryThrows_ReturnsSuccessFalse()
    {
        // Arrange
        _genericRepositoryMock
            .Setup(r => r.GetFirstOrDefault<User>(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Timeout"));

        // Act
        var result = await _sut.ChangeAsync(BuildRequest(Guid.NewGuid().ToString()));

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ChangeAsync_RepositoryThrows_DoesNotPropagateException()
    {
        // Arrange
        _genericRepositoryMock
            .Setup(r => r.GetFirstOrDefault<User>(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Timeout"));

        // Act
        var exception = await Record.ExceptionAsync(() => _sut.ChangeAsync(BuildRequest(Guid.NewGuid().ToString())));

        // Assert
        Assert.Null(exception);
    }
}
