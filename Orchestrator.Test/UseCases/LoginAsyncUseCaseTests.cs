using Microsoft.Extensions.Logging;
using Moq;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Interfaces;
using Xunit;

namespace Orchestrator.Test.UseCases;

public class LoginAsyncUseCaseTests
{
    private readonly Mock<IUserRepositoryNoSql> _userRepositoryMock;
    private readonly Mock<IRefreshTokenRepositoryNoSql> _refreshTokenRepositoryMock;
    private readonly Mock<ISecureHashingService> _hashingServiceMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IValidationService> _validationServiceMock;
    private readonly Mock<ILogger<LoginAsyncUseCase>> _loggerMock;
    private readonly LoginAsyncUseCase _sut;

    public LoginAsyncUseCaseTests()
    {
        _userRepositoryMock = new Mock<IUserRepositoryNoSql>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepositoryNoSql>();
        _hashingServiceMock = new Mock<ISecureHashingService>();
        _tokenServiceMock = new Mock<ITokenService>();
        _validationServiceMock = new Mock<IValidationService>();
        _loggerMock = new Mock<ILogger<LoginAsyncUseCase>>();

        _sut = new LoginAsyncUseCase(
            _validationServiceMock.Object,
            _loggerMock.Object,
            _userRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _hashingServiceMock.Object,
            _tokenServiceMock.Object);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static User BuildUser(
        string email = "user@example.com",
        string passwordHash = "hash",
        string salt = "salt",
        string role = "jogador")
        => User.Create("Test User", email, role, passwordHash, salt, new List<UserAssignment>(), "admin");

    private static LoginRequest BuildRequest(string email = "user@example.com", string password = "Secret123")
        => new() { Email = email, Password = password };

    private static RefreshToken BuildRefreshToken(Guid userId)
        => RefreshToken.Create(userId, "token-hash", "token-salt", DateTime.UtcNow.AddDays(7));

    private void SetupUserFound(User user)
    {
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
    }

    private void SetupUserNotFound()
    {
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<User>());
    }

    private void SetupTokenServices(User user)
    {
        var refreshToken = BuildRefreshToken(user.Id);
        _tokenServiceMock
            .Setup(t => t.CreateAccessToken(user))
            .Returns(new AccessTokenResult("access-token-value", DateTime.UtcNow.AddHours(1)));
        _tokenServiceMock
            .Setup(t => t.CreateRefreshToken(user))
            .Returns(new RefreshTokenIssueResult("raw-refresh-token", refreshToken));
        _refreshTokenRepositoryMock
            .Setup(r => r.Save(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _validationServiceMock
            .Setup(v => v.CreateValidation(It.IsAny<ValidationDto>()))
            .ReturnsAsync(true);
    }

    // ---------------------------------------------------------------
    // Happy path
    // ---------------------------------------------------------------

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsSuccessTrue()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFound(user);
        _hashingServiceMock.Setup(h => h.Verify("Secret123", user.PasswordHash, user.Salt)).Returns(true);
        SetupTokenServices(user);

        // Act
        var result = await _sut.LoginAsync(BuildRequest());

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsMessageUserLogged()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFound(user);
        _hashingServiceMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        SetupTokenServices(user);

        // Act
        var result = await _sut.LoginAsync(BuildRequest());

        // Assert
        Assert.Equal("User logged", result.Message);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAccessToken()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFound(user);
        _hashingServiceMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        SetupTokenServices(user);

        // Act
        var result = await _sut.LoginAsync(BuildRequest());

        // Assert
        Assert.Equal("access-token-value", result.AccessToken);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsRefreshToken()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFound(user);
        _hashingServiceMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        SetupTokenServices(user);

        // Act
        var result = await _sut.LoginAsync(BuildRequest());

        // Assert
        Assert.Equal("raw-refresh-token", result.RefreshToken);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsExpiresAt()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFound(user);
        _hashingServiceMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        SetupTokenServices(user);

        // Act
        var result = await _sut.LoginAsync(BuildRequest());

        // Assert
        Assert.NotNull(result.ExpiresAt);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsUserEmail()
    {
        // Arrange
        var user = BuildUser(email: "user@example.com");
        SetupUserFound(user);
        _hashingServiceMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        SetupTokenServices(user);

        // Act
        var result = await _sut.LoginAsync(BuildRequest());

        // Assert
        Assert.Equal("user@example.com", result.Email);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsUserId()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFound(user);
        _hashingServiceMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        SetupTokenServices(user);

        // Act
        var result = await _sut.LoginAsync(BuildRequest());

        // Assert
        Assert.Equal(user.Id.ToString(), result.UserId);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsRole()
    {
        // Arrange
        var user = BuildUser(role: "adm");
        SetupUserFound(user);
        _hashingServiceMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        SetupTokenServices(user);

        // Act
        var result = await _sut.LoginAsync(BuildRequest());

        // Assert
        Assert.Equal("adm", result.Role);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsMustChangePasswordFalse()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFound(user);
        _hashingServiceMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        SetupTokenServices(user);

        // Act
        var result = await _sut.LoginAsync(BuildRequest());

        // Assert
        Assert.False(result.MustChangePassword);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_PersistsRefreshTokenViaRepository()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFound(user);
        _hashingServiceMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        SetupTokenServices(user);

        // Act
        await _sut.LoginAsync(BuildRequest());

        // Assert
        _refreshTokenRepositoryMock.Verify(r => r.Save(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_CallsCreateValidation()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFound(user);
        _hashingServiceMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        SetupTokenServices(user);

        // Act
        await _sut.LoginAsync(BuildRequest());

        // Assert
        _validationServiceMock.Verify(v => v.CreateValidation(It.IsAny<ValidationDto>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_CreateValidationReceivesCorrectUserId()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFound(user);
        _hashingServiceMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        ValidationDto? capturedDto = null;
        var refreshToken = BuildRefreshToken(user.Id);
        _tokenServiceMock.Setup(t => t.CreateAccessToken(user)).Returns(new AccessTokenResult("tok", DateTime.UtcNow.AddHours(1)));
        _tokenServiceMock.Setup(t => t.CreateRefreshToken(user)).Returns(new RefreshTokenIssueResult("raw", refreshToken));
        _refreshTokenRepositoryMock.Setup(r => r.Save(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _validationServiceMock
            .Setup(v => v.CreateValidation(It.IsAny<ValidationDto>()))
            .Callback<ValidationDto>(dto => capturedDto = dto)
            .ReturnsAsync(true);

        // Act
        await _sut.LoginAsync(BuildRequest());

        // Assert
        Assert.NotNull(capturedDto);
        Assert.Equal(user.Id.ToString(), capturedDto!.UserId);
        Assert.Equal(user.Email, capturedDto.UserEmail);
    }

    // ---------------------------------------------------------------
    // User not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task LoginAsync_UserNotFound_ReturnsSuccessFalse()
    {
        // Arrange
        SetupUserNotFound();

        // Act
        var result = await _sut.LoginAsync(BuildRequest());

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ReturnsMessageInvalidCredentials()
    {
        // Arrange
        SetupUserNotFound();

        // Act
        var result = await _sut.LoginAsync(BuildRequest());

        // Assert
        Assert.Equal("Invalid credentials", result.Message);
    }

    // ---------------------------------------------------------------
    // Hash verification fails
    // ---------------------------------------------------------------

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsSuccessFalse()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFound(user);
        _hashingServiceMock
            .Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        // Act
        var result = await _sut.LoginAsync(BuildRequest(password: "WrongPass!"));

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsMessageInvalidCredentials()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFound(user);
        _hashingServiceMock
            .Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        // Act
        var result = await _sut.LoginAsync(BuildRequest(password: "WrongPass!"));

        // Assert
        Assert.Equal("Invalid credentials", result.Message);
    }

    // ---------------------------------------------------------------
    // Exception
    // ---------------------------------------------------------------

    [Fact]
    public async Task LoginAsync_RepositoryThrows_ReturnsSuccessFalse()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        // Act
        var result = await _sut.LoginAsync(BuildRequest());

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task LoginAsync_RepositoryThrows_DoesNotPropagateException()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        // Act
        var exception = await Record.ExceptionAsync(() => _sut.LoginAsync(BuildRequest()));

        // Assert
        Assert.Null(exception);
    }
}
