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

    // ---------------------------------------------------------------
    // Detecção de reuso: token revogado voltando derruba a cadeia
    // ---------------------------------------------------------------

    /// <summary>
    /// Um usuário com um token ativo e um já rotacionado, com hashes distintos — sem isso os
    /// dublês de <c>Verify</c> não conseguiriam distinguir um do outro.
    /// </summary>
    private (User User, RefreshToken Active, RefreshToken Replayed) SetupReplayScenario()
    {
        var user = BuildUser();

        var active = RefreshToken.Create(user.Id, "hash-ativo", "salt-ativo", DateTime.UtcNow.AddDays(7));
        var replayed = RefreshToken.Create(user.Id, "hash-gasto", "salt-gasto", DateTime.UtcNow.AddDays(7));
        replayed.Revoke("Rotated", active.Id);

        _userRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });

        _refreshTokenRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<System.Linq.Expressions.Expression<Func<RefreshToken, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { active, replayed });

        // O valor apresentado é o do token JÁ GASTO.
        _hashingServiceMock
            .Setup(h => h.Verify("raw-vazado", "hash-gasto", "salt-gasto"))
            .Returns(true);
        _hashingServiceMock
            .Setup(h => h.Verify("raw-vazado", "hash-ativo", "salt-ativo"))
            .Returns(false);

        _refreshTokenRepositoryMock
            .Setup(r => r.Update(It.IsAny<string>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return (user, active, replayed);
    }

    [Fact]
    public async Task RefreshAsync_ReusedRevokedToken_RevokesEveryActiveTokenOfTheUser()
    {
        // Um token já rotacionado voltando significa que o valor circulou depois de gasto. Não dá
        // para distinguir "cliente repetiu a chamada" de "alguém copiou", e o custo dos dois
        // enganos é assimétrico: pedir login de novo incomoda; deixar renovar por 30 dias é a
        // conta perdida.
        var (user, active, _) = SetupReplayScenario();

        var result = await _sut.RefreshAsync(
            new RefreshTokenRequest { UserId = user.Id.ToString(), RefreshToken = "raw-vazado" });

        Assert.False(result.Success);
        Assert.Equal("Invalid refresh token", result.Message);
        Assert.False(active.IsActive);
        Assert.Equal("Reuse detected", active.ReasonRevoked);
        _refreshTokenRepositoryMock.Verify(
            r => r.Update(active.Id.ToString(), active, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_ReusedRevokedToken_IssuesNoNewCredentials()
    {
        var (user, _, _) = SetupReplayScenario();

        await _sut.RefreshAsync(
            new RefreshTokenRequest { UserId = user.Id.ToString(), RefreshToken = "raw-vazado" });

        _refreshTokenRepositoryMock.Verify(
            r => r.Save(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _tokenServiceMock.Verify(t => t.CreateAccessToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_ReusedRevokedToken_LogsAWarning()
    {
        var (user, _, _) = SetupReplayScenario();

        await _sut.RefreshAsync(
            new RefreshTokenRequest { UserId = user.Id.ToString(), RefreshToken = "raw-vazado" });

        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("reuse detected")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_HappyPath_NeverHashesAgainstRevokedTokens()
    {
        // Os revogados se acumulam a cada rotação e cada teste é um PBKDF2 de 100.000 iterações.
        // Percorrê-los no caminho feliz faria o custo de um refresh crescer para sempre.
        var user = BuildUser();
        var active = RefreshToken.Create(user.Id, "hash-ativo", "salt-ativo", DateTime.UtcNow.AddDays(7));
        var oldOne = RefreshToken.Create(user.Id, "hash-antigo", "salt-antigo", DateTime.UtcNow.AddDays(7));
        oldOne.Revoke("Rotated", active.Id);

        _userRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        _refreshTokenRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<System.Linq.Expressions.Expression<Func<RefreshToken, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { oldOne, active });
        _hashingServiceMock.Setup(h => h.Verify("raw-incoming", "hash-ativo", "salt-ativo")).Returns(true);
        _tokenServiceMock
            .Setup(t => t.CreateAccessToken(user))
            .Returns(new AccessTokenResult("new-access-token", DateTime.UtcNow.AddHours(1)));
        _tokenServiceMock
            .Setup(t => t.CreateRefreshToken(user))
            .Returns(new RefreshTokenIssueResult(
                "new-raw", RefreshToken.Create(user.Id, "h", "s", DateTime.UtcNow.AddDays(30))));
        _refreshTokenRepositoryMock
            .Setup(r => r.Update(It.IsAny<string>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _refreshTokenRepositoryMock
            .Setup(r => r.Save(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.RefreshAsync(
            new RefreshTokenRequest { UserId = user.Id.ToString(), RefreshToken = "raw-incoming" });

        Assert.True(result.Success);
        _hashingServiceMock.Verify(
            h => h.Verify(It.IsAny<string>(), "hash-antigo", "salt-antigo"), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_UnknownToken_LogsAWarningWithoutRevokingAnything()
    {
        var user = BuildUser();
        var active = RefreshToken.Create(user.Id, "hash-ativo", "salt-ativo", DateTime.UtcNow.AddDays(7));

        _userRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        _refreshTokenRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<System.Linq.Expressions.Expression<Func<RefreshToken, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { active });
        _hashingServiceMock
            .Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        var result = await _sut.RefreshAsync(
            new RefreshTokenRequest { UserId = user.Id.ToString(), RefreshToken = "palpite" });

        // Palpite errado não é replay: a sessão legítima do usuário continua de pé, senão
        // qualquer um derrubaria a sessão de qualquer outro mandando lixo neste endpoint.
        Assert.False(result.Success);
        Assert.True(active.IsActive);
        _refreshTokenRepositoryMock.Verify(
            r => r.Update(It.IsAny<string>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
