using Microsoft.Extensions.Logging;
using Moq;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases;
using Xunit;

namespace Orchestrator.Test.UseCases;

public class GetUserUseCaseTests
{
    private readonly Mock<IUserRepositoryNoSql> _userRepositoryMock;
    private readonly Mock<ILogger<GetUserUseCase>> _loggerMock;
    private readonly GetUserUseCase _sut;

    public GetUserUseCaseTests()
    {
        _userRepositoryMock = new Mock<IUserRepositoryNoSql>();
        _loggerMock = new Mock<ILogger<GetUserUseCase>>();
        _sut = new GetUserUseCase(_userRepositoryMock.Object, _loggerMock.Object);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static User BuildUser(
        string name = "Test User",
        string email = "test@example.com",
        string role = "jogador")
        => User.Create(name, email, role, "hash", "salt", new List<UserAssignment>(), "admin");

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

    // ---------------------------------------------------------------
    // Happy path
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAsync_UserExists_ReturnsNonNullResponse()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFound(user);

        // Act
        var result = await _sut.GetAsync(user.Id.ToString());

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAsync_UserExists_ReturnsCorrectId()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFound(user);

        // Act
        var result = await _sut.GetAsync(user.Id.ToString());

        // Assert
        Assert.Equal(user.Id.ToString(), result!.Id);
    }

    [Fact]
    public async Task GetAsync_UserExists_ReturnsCorrectName()
    {
        // Arrange
        var user = BuildUser(name: "Alice");
        SetupUserFound(user);

        // Act
        var result = await _sut.GetAsync(user.Id.ToString());

        // Assert
        Assert.Equal("Alice", result!.Name);
    }

    [Fact]
    public async Task GetAsync_UserExists_ReturnsCorrectEmail()
    {
        // Arrange
        var user = BuildUser(email: "alice@example.com");
        SetupUserFound(user);

        // Act
        var result = await _sut.GetAsync(user.Id.ToString());

        // Assert
        Assert.Equal("alice@example.com", result!.Email);
    }

    [Fact]
    public async Task GetAsync_UserExists_ReturnsCorrectRole()
    {
        // Arrange
        var user = BuildUser(role: "adm");
        SetupUserFound(user);

        // Act
        var result = await _sut.GetAsync(user.Id.ToString());

        // Assert
        Assert.Equal("adm", result!.Role);
    }

    [Fact]
    public async Task GetAsync_UserExists_ReturnsMustChangePasswordFalse()
    {
        // Arrange
        var user = BuildUser();
        SetupUserFound(user);

        // Act
        var result = await _sut.GetAsync(user.Id.ToString());

        // Assert
        Assert.False(result!.MustChangePassword);
    }

    [Fact]
    public async Task GetAsync_UserWithAssignments_MapsAssignmentsCorrectly()
    {
        // Arrange
        var nodes = new List<UserAssignment.HierarchyNode>
        {
            UserAssignment.HierarchyNode.Create("node-1", "Node One")
        };
        var assignment = UserAssignment.Create("Team A", "team-1", "Player", "role-1", nodes, "admin");
        var user = User.Create("Alice", "alice@example.com", "jogador", "hash", "salt",
            new List<UserAssignment> { assignment }, "admin");
        SetupUserFound(user);

        // Act
        var result = await _sut.GetAsync(user.Id.ToString());

        // Assert
        Assert.NotNull(result);
        Assert.Single(result!.Assignments);
        Assert.Equal("Team A", result.Assignments[0].TeamName);
        Assert.Equal("team-1", result.Assignments[0].TeamId);
        Assert.Equal("node-1", result.Assignments[0].HierarchyNodes[0].NodeId);
        Assert.Equal("Node One", result.Assignments[0].HierarchyNodes[0].NodeName);
    }

    // ---------------------------------------------------------------
    // User not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAsync_UserNotFound_ReturnsNull()
    {
        // Arrange
        SetupUserNotFound();

        // Act
        var result = await _sut.GetAsync(Guid.NewGuid().ToString());

        // Assert
        Assert.Null(result);
    }

    // ---------------------------------------------------------------
    // Repository throws
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAsync_RepositoryThrows_ReturnsNull()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection error"));

        // Act
        var result = await _sut.GetAsync(Guid.NewGuid().ToString());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_RepositoryThrows_DoesNotPropagateException()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection error"));

        // Act
        var exception = await Record.ExceptionAsync(() => _sut.GetAsync(Guid.NewGuid().ToString()));

        // Assert
        Assert.Null(exception);
    }
}
