using Microsoft.Extensions.Logging;
using Moq;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Dto;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Interfaces;
using Xunit;

namespace Orchestrator.Test.UseCases;

public class CreateUserUseCaseTests
{
    private readonly Mock<IUserRepositoryNoSql> _userRepositoryMock;
    private readonly Mock<ISecureHashingService> _hashingServiceMock;
    private readonly Mock<ILogger<CreateUserUseCase>> _loggerMock;
    private readonly CreateUserUseCase _sut;

    public CreateUserUseCaseTests()
    {
        _userRepositoryMock = new Mock<IUserRepositoryNoSql>();
        _hashingServiceMock = new Mock<ISecureHashingService>();
        _loggerMock = new Mock<ILogger<CreateUserUseCase>>();
        _sut = new CreateUserUseCase(
            _userRepositoryMock.Object,
            _hashingServiceMock.Object,
            _loggerMock.Object);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static CreateUserRequest BuildRequest(
        string email = "user@example.com",
        string role = "jogador",
        string name = "Test User",
        string password = "Secret123",
        string createdBy = "admin")
        => new()
        {
            Name = name,
            Email = email,
            Password = password,
            PasswordConfirmation = password,
            Role = role,
            CreatedBy = createdBy,
            Assignments = new List<UserAssignmentDto>()
        };

    private void SetupNoExistingUser()
    {
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<User>());
    }

    private void SetupHashingService(string hash = "hash-value", string salt = "salt-value")
    {
        _hashingServiceMock
            .Setup(h => h.HashValue(It.IsAny<string>()))
            .Returns((hash, salt));
    }

    // ---------------------------------------------------------------
    // Happy path
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsSuccessTrue()
    {
        // Arrange
        SetupNoExistingUser();
        SetupHashingService();
        _userRepositoryMock.Setup(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var req = BuildRequest();

        // Act
        var result = await _sut.CreateAsync(req);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsMessageUserCreated()
    {
        // Arrange
        SetupNoExistingUser();
        SetupHashingService();
        _userRepositoryMock.Setup(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CreateAsync(BuildRequest());

        // Assert
        Assert.Equal("User created", result.Message);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsNonNullUserId()
    {
        // Arrange
        SetupNoExistingUser();
        SetupHashingService();
        _userRepositoryMock.Setup(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CreateAsync(BuildRequest());

        // Assert
        Assert.NotNull(result.UserId);
        Assert.NotEmpty(result.UserId!);
    }

    [Fact]
    public async Task CreateAsync_EmailWithUpperCaseAndSpaces_NormalizesToLowerTrimmed()
    {
        // Arrange
        SetupNoExistingUser();
        SetupHashingService();

        User? savedUser = null;
        _userRepositoryMock
            .Setup(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => savedUser = u)
            .Returns(Task.CompletedTask);

        var req = BuildRequest(email: "  USER@Example.COM  ");

        // Act
        await _sut.CreateAsync(req);

        // Assert
        Assert.NotNull(savedUser);
        Assert.Equal("user@example.com", savedUser!.Email);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CallsSaveOnRepository()
    {
        // Arrange
        SetupNoExistingUser();
        SetupHashingService();
        _userRepositoryMock.Setup(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CreateAsync(BuildRequest());

        // Assert
        _userRepositoryMock.Verify(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_AssignmentsMappedCorrectly()
    {
        // Arrange
        SetupNoExistingUser();
        SetupHashingService();

        User? savedUser = null;
        _userRepositoryMock
            .Setup(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => savedUser = u)
            .Returns(Task.CompletedTask);

        var req = BuildRequest();
        req.Assignments = new List<UserAssignmentDto>
        {
            new()
            {
                TeamName = "Team A",
                TeamId = "team-1",
                RoleName = "Player",
                RoleId = "role-1",
                HierarchyNodes = new List<HierarchyNodeDto>
                {
                    new() { NodeId = "node-1", NodeName = "Node One" }
                }
            }
        };

        // Act
        await _sut.CreateAsync(req);

        // Assert
        Assert.NotNull(savedUser);
        Assert.Single(savedUser!.Assignments);
        Assert.Equal("Team A", savedUser.Assignments[0].TeamName);
        Assert.Equal("node-1", savedUser.Assignments[0].HierarchyNodes[0].NodeId);
    }

    // ---------------------------------------------------------------
    // Existing user with same email
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_EmailAlreadyExists_ReturnsSuccessFalse()
    {
        // Arrange
        var existingUser = User.Create("Existing", "user@example.com", "jogador", "hash", "salt", new List<UserAssignment>(), "admin");
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existingUser });

        // Act
        var result = await _sut.CreateAsync(BuildRequest());

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateAsync_EmailAlreadyExists_ReturnsMessageUserAlreadyHasAccount()
    {
        // Arrange
        var existingUser = User.Create("Existing", "user@example.com", "jogador", "hash", "salt", new List<UserAssignment>(), "admin");
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existingUser });

        // Act
        var result = await _sut.CreateAsync(BuildRequest());

        // Assert
        Assert.Equal("User already has a account", result.Message);
    }

    // ---------------------------------------------------------------
    // Invalid role
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("unknown-role")]
    [InlineData("")]
    [InlineData("ADMIN")]    // case-sensitive mismatch — "adm" is valid, "ADMIN" is not
    public async Task CreateAsync_InvalidRole_ReturnsSuccessFalse(string invalidRole)
    {
        // Arrange
        SetupNoExistingUser();

        // Act
        var result = await _sut.CreateAsync(BuildRequest(role: invalidRole));

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateAsync_InvalidRole_ReturnsMessageInvalidRole()
    {
        // Arrange
        SetupNoExistingUser();

        // Act
        var result = await _sut.CreateAsync(BuildRequest(role: "not-a-valid-role"));

        // Assert
        Assert.Equal("Invalid role", result.Message);
    }

    // ---------------------------------------------------------------
    // Valid roles (normalisation)
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("jogador", "jogador")]
    [InlineData("JOGADOR", "jogador")]        // case-insensitive lookup
    [InlineData("adm", "adm")]
    [InlineData("super adm", "super adm")]
    [InlineData("lider de time", "lider de time")]
    [InlineData("jogador principal", "jogador principal")]
    public async Task CreateAsync_ValidRole_NormalizesRoleCorrectly(string inputRole, string expectedNormalizedRole)
    {
        // Arrange
        SetupNoExistingUser();
        SetupHashingService();

        User? savedUser = null;
        _userRepositoryMock
            .Setup(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => savedUser = u)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CreateAsync(BuildRequest(role: inputRole));

        // Assert
        Assert.NotNull(savedUser);
        Assert.Equal(expectedNormalizedRole, savedUser!.Role);
    }

    // ---------------------------------------------------------------
    // Exception during save
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_RepositoryThrows_ReturnsSuccessFalse()
    {
        // Arrange
        SetupNoExistingUser();
        SetupHashingService();
        _userRepositoryMock
            .Setup(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB connection lost"));

        // Act
        var result = await _sut.CreateAsync(BuildRequest());

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateAsync_RepositoryThrows_DoesNotPropagateException()
    {
        // Arrange
        SetupNoExistingUser();
        SetupHashingService();
        _userRepositoryMock
            .Setup(r => r.Save(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB connection lost"));

        // Act
        var exception = await Record.ExceptionAsync(() => _sut.CreateAsync(BuildRequest()));

        // Assert
        Assert.Null(exception);
    }
}
