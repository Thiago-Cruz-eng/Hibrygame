using Microsoft.Extensions.Logging;
using Moq;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Interfaces;
using Xunit;

namespace Orchestrator.Test.UseCases;

public class RefreshTokenUseCaseTests
{
    private readonly Mock<IUserRepositoryNoSql> _userRepositoryMock;
    private readonly Mock<IRefreshTokenRepositoryNoSql> _refreshTokenRepositoryMock;
    private readonly Mock<ISecureHashingService> _hashingServiceMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ILogger<RefreshTokenUseCase>> _loggerMock;
    private readonly RefreshTokenUseCase _sut;

    public RefreshTokenUseCaseTests()
    {
        _userRepositoryMock = new Mock<IUserRepositoryNoSql>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepositoryNoSql>();
        _hashingServiceMock = new Mock<ISecureHashingService>();
        _tokenServiceMock = new Mock<ITokenService>();
        _loggerMock = new Mock<ILogger<RefreshTokenUseCase>>();

        _sut = new RefreshTokenUseCase(
            _userRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _hashingServiceMock.Object,
            _tokenServiceMock.Object,
            _loggerMock.Object);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static User BuildUser()
        => User.Create("Test User", "test@example.com", "jogador", "hash", "salt", new List<UserAssignment>(), "admin");

    private static RefreshToken BuildActiveRefreshToken(Guid userId)
        => RefreshToken.Create(userId, "stored-hash", "stored-salt", DateTime.UtcNow.AddDays(7));

    private static RefreshToken BuildExpiredRefreshToken(Guid userId)
        => RefreshToken.Create(userId, "stored-hash", "stored-salt", DateTime.UtcNow.AddDays(-1));

    private static RefreshToken BuildRevokedRefreshToken(Guid userId)
    {
        var token = RefreshToken.Create(userId, "stored-hash", "stored-salt", DateTime.UtcNow.AddDays(7));
        token.Revoke("Rotated");
        return token;
    }

    private void SetupFullHappyPath(User user, RefreshToken activeToken, RefreshToken newToken)
    {
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });

        _refreshTokenRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { activeToken });

        // The raw token "raw-incoming" matches the stored token
        _hashingServiceMock
            .Setup(h => h.Verify("raw-incoming", activeToken.TokenHash, activeToken.Salt))
            .Returns(true);

        _tokenServiceMock
            .Setup(t => t.CreateAccessToken(user))
            .Returns(new AccessTokenResult("new-access-token", DateTime.UtcNow.AddHours(1)));

        _tokenServiceMock
            .Setup(t => t.CreateRefreshToken(user))
            .Returns(new RefreshTokenIssueResult("new-raw-refresh", newToken));

        _refreshTokenRepositoryMock
            .Setup(r => r.Update(It.IsAny<string>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _refreshTokenRepositoryMock
            .Setup(r => r.Save(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ---------------------------------------------------------------
    // Happy path
    // ---------------------------------------------------------------

    [Fact]
    public async Task RefreshAsync_ValidToken_ReturnsSuccessTrue()
    {
        // Arrange
        var user = BuildUser();
        var activeToken = BuildActiveRefreshToken(user.Id);
        var newToken = BuildActiveRefreshToken(user.Id);
        SetupFullHappyPath(user, activeToken, newToken);

        var req = new RefreshTokenRequest { UserId = user.Id.ToString(), RefreshToken = "raw-incoming" };

        // Act
        var result = await _sut.RefreshAsync(req);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_ReturnsMessageTokenRefreshed()
    {
        // Arrange
        var user = BuildUser();
        var activeToken = BuildActiveRefreshToken(user.Id);
        var newToken = BuildActiveRefreshToken(user.Id);
        SetupFullHappyPath(user, activeToken, newToken);

        var req = new RefreshTokenRequest { UserId = user.Id.ToString(), RefreshToken = "raw-incoming" };

        // Act
        var result = await _sut.RefreshAsync(req);

        // Assert
        Assert.Equal("Token refreshed", result.Message);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_ReturnsNewAccessToken()
    {
        // Arrange
        var user = BuildUser();
        var activeToken = BuildActiveRefreshToken(user.Id);
        var newToken = BuildActiveRefreshToken(user.Id);
        SetupFullHappyPath(user, activeToken, newToken);

        var req = new RefreshTokenRequest { UserId = user.Id.ToString(), RefreshToken = "raw-incoming" };

        // Act
        var result = await _sut.RefreshAsync(req);

        // Assert
        Assert.Equal("new-access-token", result.AccessToken);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_ReturnsNewRawRefreshToken()
    {
        // Arrange
        var user = BuildUser();
        var activeToken = BuildActiveRefreshToken(user.Id);
        var newToken = BuildActiveRefreshToken(user.Id);
        SetupFullHappyPath(user, activeToken, newToken);

        var req = new RefreshTokenRequest { UserId = user.Id.ToString(), RefreshToken = "raw-incoming" };

        // Act
        var result = await _sut.RefreshAsync(req);

        // Assert
        Assert.Equal("new-raw-refresh", result.RefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_RevokesOldRefreshToken()
    {
        // Arrange
        var user = BuildUser();
        var activeToken = BuildActiveRefreshToken(user.Id);
        var newToken = BuildActiveRefreshToken(user.Id);
        SetupFullHappyPath(user, activeToken, newToken);

        var req = new RefreshTokenRequest { UserId = user.Id.ToString(), RefreshToken = "raw-incoming" };

        // Act
        await _sut.RefreshAsync(req);

        // Assert — old token must be updated (revoked) then new one saved
        _refreshTokenRepositoryMock.Verify(
            r => r.Update(activeToken.Id.ToString(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_SavesNewRefreshToken()
    {
        // Arrange
        var user = BuildUser();
        var activeToken = BuildActiveRefreshToken(user.Id);
        var newToken = BuildActiveRefreshToken(user.Id);
        SetupFullHappyPath(user, activeToken, newToken);

        var req = new RefreshTokenRequest { UserId = user.Id.ToString(), RefreshToken = "raw-incoming" };

        // Act
        await _sut.RefreshAsync(req);

        // Assert
        _refreshTokenRepositoryMock.Verify(r => r.Save(newToken, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------
    // Invalid Guid userId
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("12345")]
    public async Task RefreshAsync_InvalidGuidUserId_ReturnsSuccessFalse(string invalidUserId)
    {
        // Arrange
        var req = new RefreshTokenRequest { UserId = invalidUserId, RefreshToken = "some-token" };

        // Act
        var result = await _sut.RefreshAsync(req);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task RefreshAsync_InvalidGuidUserId_ReturnsMessageInvalidUser()
    {
        // Arrange
        var req = new RefreshTokenRequest { UserId = "bad-guid", RefreshToken = "some-token" };

        // Act
        var result = await _sut.RefreshAsync(req);

        // Assert
        Assert.Equal("Invalid user", result.Message);
    }

    // ---------------------------------------------------------------
    // User not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task RefreshAsync_UserNotFound_ReturnsSuccessFalse()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<User>());

        var req = new RefreshTokenRequest { UserId = Guid.NewGuid().ToString(), RefreshToken = "some-token" };

        // Act
        var result = await _sut.RefreshAsync(req);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task RefreshAsync_UserNotFound_ReturnsMessageUserNotFound()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<User>());

        var req = new RefreshTokenRequest { UserId = Guid.NewGuid().ToString(), RefreshToken = "some-token" };

        // Act
        var result = await _sut.RefreshAsync(req);

        // Assert
        Assert.Equal("User not found", result.Message);
    }

    // ---------------------------------------------------------------
    // No matching refresh token
    // ---------------------------------------------------------------

    [Fact]
    public async Task RefreshAsync_NoMatchingRefreshToken_ReturnsSuccessFalse()
    {
        // Arrange
        var user = BuildUser();
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });

        // No tokens exist for this user
        _refreshTokenRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<RefreshToken>());

        var req = new RefreshTokenRequest { UserId = user.Id.ToString(), RefreshToken = "unknown-raw-token" };

        // Act
        var result = await _sut.RefreshAsync(req);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task RefreshAsync_NoMatchingRefreshToken_ReturnsMessageInvalidRefreshToken()
    {
        // Arrange
        var user = BuildUser();
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });

        _refreshTokenRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<RefreshToken>());

        _hashingServiceMock
            .Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        var req = new RefreshTokenRequest { UserId = user.Id.ToString(), RefreshToken = "unknown-raw-token" };

        // Act
        var result = await _sut.RefreshAsync(req);

        // Assert
        Assert.Equal("Invalid refresh token", result.Message);
    }

    // ---------------------------------------------------------------
    // Matching but inactive (revoked) token
    // ---------------------------------------------------------------

    [Fact]
    public async Task RefreshAsync_RevokedMatchingToken_ReturnsSuccessFalse()
    {
        // Arrange
        var user = BuildUser();
        var revokedToken = BuildRevokedRefreshToken(user.Id);

        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });

        _refreshTokenRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { revokedToken });

        // Hash matches but token is not active
        _hashingServiceMock
            .Setup(h => h.Verify("raw-incoming", revokedToken.TokenHash, revokedToken.Salt))
            .Returns(true);

        var req = new RefreshTokenRequest { UserId = user.Id.ToString(), RefreshToken = "raw-incoming" };

        // Act
        var result = await _sut.RefreshAsync(req);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task RefreshAsync_ExpiredMatchingToken_ReturnsSuccessFalse()
    {
        // Arrange
        var user = BuildUser();
        var expiredToken = BuildExpiredRefreshToken(user.Id);

        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });

        _refreshTokenRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { expiredToken });

        _hashingServiceMock
            .Setup(h => h.Verify("raw-incoming", expiredToken.TokenHash, expiredToken.Salt))
            .Returns(true);

        var req = new RefreshTokenRequest { UserId = user.Id.ToString(), RefreshToken = "raw-incoming" };

        // Act
        var result = await _sut.RefreshAsync(req);

        // Assert
        Assert.False(result.Success);
    }

    // ---------------------------------------------------------------
    // Exception
    // ---------------------------------------------------------------

    [Fact]
    public async Task RefreshAsync_RepositoryThrows_ReturnsSuccessFalse()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Timeout"));

        var req = new RefreshTokenRequest { UserId = Guid.NewGuid().ToString(), RefreshToken = "some-token" };

        // Act
        var result = await _sut.RefreshAsync(req);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task RefreshAsync_RepositoryThrows_DoesNotPropagateException()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Timeout"));

        var req = new RefreshTokenRequest { UserId = Guid.NewGuid().ToString(), RefreshToken = "some-token" };

        // Act
        var exception = await Record.ExceptionAsync(() => _sut.RefreshAsync(req));

        // Assert
        Assert.Null(exception);
    }
}
