using Microsoft.Extensions.Logging;
using Moq;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases;
using Xunit;

namespace Orchestrator.Test.UseCases;

public class DeleteUserUseCaseTests
{
    private readonly Mock<IUserRepositoryNoSql> _userRepositoryMock;
    private readonly Mock<ILogger<DeleteUserUseCase>> _loggerMock;
    private readonly DeleteUserUseCase _sut;

    public DeleteUserUseCaseTests()
    {
        _userRepositoryMock = new Mock<IUserRepositoryNoSql>();
        _loggerMock = new Mock<ILogger<DeleteUserUseCase>>();
        _sut = new DeleteUserUseCase(_userRepositoryMock.Object, _loggerMock.Object);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static User BuildUser()
        => User.Create("Test User", "test@example.com", "jogador", "hash", "salt", new List<UserAssignment>(), "admin");

    // ---------------------------------------------------------------
    // Happy path
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_UserExistsAndDeleteSucceeds_ReturnsSuccessTrue()
    {
        // Arrange
        var user = BuildUser();
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        _userRepositoryMock
            .Setup(r => r.Delete(user.Id.ToString(), user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.DeleteAsync(user.Id.ToString());

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task DeleteAsync_UserExistsAndDeleteSucceeds_ReturnsMessageUserDeleted()
    {
        // Arrange
        var user = BuildUser();
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        _userRepositoryMock
            .Setup(r => r.Delete(user.Id.ToString(), user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.DeleteAsync(user.Id.ToString());

        // Assert
        Assert.Equal("User deleted", result.Message);
    }

    [Fact]
    public async Task DeleteAsync_UserExistsAndDeleteSucceeds_CallsDeleteOnRepository()
    {
        // Arrange
        var user = BuildUser();
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        _userRepositoryMock
            .Setup(r => r.Delete(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.DeleteAsync(user.Id.ToString());

        // Assert
        _userRepositoryMock.Verify(r => r.Delete(user.Id.ToString(), user, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------
    // User not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_UserNotFound_ReturnsSuccessFalse()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<User>());

        // Act
        var result = await _sut.DeleteAsync(Guid.NewGuid().ToString());

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task DeleteAsync_UserNotFound_ReturnsMessageUserNotFound()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<User>());

        // Act
        var result = await _sut.DeleteAsync(Guid.NewGuid().ToString());

        // Assert
        Assert.Equal("User not found", result.Message);
    }

    // ---------------------------------------------------------------
    // Repository.Delete returns false
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_DeleteReturnsFalse_ReturnsSuccessFalse()
    {
        // Arrange
        var user = BuildUser();
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        _userRepositoryMock
            .Setup(r => r.Delete(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.DeleteAsync(user.Id.ToString());

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task DeleteAsync_DeleteReturnsFalse_ReturnsMessageUserNotDeleted()
    {
        // Arrange
        var user = BuildUser();
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        _userRepositoryMock
            .Setup(r => r.Delete(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.DeleteAsync(user.Id.ToString());

        // Assert
        Assert.Equal("User not deleted", result.Message);
    }

    // ---------------------------------------------------------------
    // Exception
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_RepositoryThrows_ReturnsSuccessFalse()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection dropped"));

        // Act
        var result = await _sut.DeleteAsync(Guid.NewGuid().ToString());

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task DeleteAsync_RepositoryThrows_DoesNotPropagateException()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection dropped"));

        // Act
        var exception = await Record.ExceptionAsync(() => _sut.DeleteAsync(Guid.NewGuid().ToString()));

        // Assert
        Assert.Null(exception);
    }
}
