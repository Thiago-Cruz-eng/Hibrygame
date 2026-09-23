using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Security.Authorization;
using Xunit;

namespace Orchestrator.Test.UseCases;

/// <summary>
/// Remoção de usuário.
///
/// <para>
/// Além do caminho antigo, os testes novos guardam duas regras de segurança: ninguém apaga quem
/// está acima do próprio nível, e a remoção leva junto os refresh tokens e os registros de
/// validação do usuário — que antes ficavam órfãos.
/// </para>
/// </summary>
public class DeleteUserUseCaseTests
{
    private readonly Mock<IUserRepositoryNoSql> _userRepositoryMock;
    private readonly Mock<IRefreshTokenRepositoryNoSql> _refreshTokenRepositoryMock;
    private readonly Mock<IValidationRepositoryNoSql> _validationRepositoryMock;
    private readonly Mock<ILogger<DeleteUserUseCase>> _loggerMock;
    private readonly DeleteUserUseCase _sut;

    public DeleteUserUseCaseTests()
    {
        _userRepositoryMock = new Mock<IUserRepositoryNoSql>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepositoryNoSql>();
        _validationRepositoryMock = new Mock<IValidationRepositoryNoSql>();
        _loggerMock = new Mock<ILogger<DeleteUserUseCase>>();

        _refreshTokenRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RefreshToken>());
        _refreshTokenRepositoryMock
            .Setup(r => r.Update(It.IsAny<string>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _validationRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<Expression<Func<Validation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Validation>());
        _validationRepositoryMock
            .Setup(r => r.Delete(It.IsAny<string>(), It.IsAny<Validation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _sut = new DeleteUserUseCase(
            _userRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _validationRepositoryMock.Object,
            _loggerMock.Object);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static User BuildUser(string role = "jogador")
        => User.Create("Test User", "test@example.com", role, "hash", "salt", new List<UserAssignment>(), "admin");

    private void SetupUserFound(User user)
        => _userRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });

    private void SetupDeleteResult(bool deleted)
        => _userRepositoryMock
            .Setup(r => r.Delete(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(deleted);

    /// <summary>Um <c>adm</c> apagando: a alçada normal deste endpoint.</summary>
    private Task<Orchestrator.UseCases.Dto.Response.DeleteUserResponse> DeleteAsAdmin(string id)
        => _sut.DeleteAsync(id, RoleLevel.Admin, "adm-1");

    // ---------------------------------------------------------------
    // Happy path
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_UserExistsAndDeleteSucceeds_ReturnsSuccessTrue()
    {
        var user = BuildUser();
        SetupUserFound(user);
        SetupDeleteResult(true);

        var result = await DeleteAsAdmin(user.Id.ToString());

        Assert.True(result.Success);
    }

    [Fact]
    public async Task DeleteAsync_UserExistsAndDeleteSucceeds_ReturnsMessageUserDeleted()
    {
        var user = BuildUser();
        SetupUserFound(user);
        SetupDeleteResult(true);

        var result = await DeleteAsAdmin(user.Id.ToString());

        Assert.Equal("User deleted", result.Message);
    }

    [Fact]
    public async Task DeleteAsync_UserExistsAndDeleteSucceeds_CallsDeleteOnRepository()
    {
        var user = BuildUser();
        SetupUserFound(user);
        SetupDeleteResult(true);

        await DeleteAsAdmin(user.Id.ToString());

        _userRepositoryMock.Verify(
            r => r.Delete(user.Id.ToString(), user, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------
    // Alçada: ninguém apaga quem está acima de si
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_TargetOutranksTheCaller_IsRejected()
    {
        // A policy do endpoint garante apenas que o chamador é ao menos "adm"; ela não sabe quem
        // ele está tentando apagar. Sem esta checagem, um "adm" apagava um "super adm".
        var user = BuildUser("super adm");
        SetupUserFound(user);
        SetupDeleteResult(true);

        var result = await DeleteAsAdmin(user.Id.ToString());

        Assert.False(result.Success);
        Assert.Equal("Cannot delete a user with a role above your own.", result.Message);
        _userRepositoryMock.Verify(
            r => r.Delete(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_TargetAtTheSameLevel_IsAllowed()
    {
        var user = BuildUser("adm");
        SetupUserFound(user);
        SetupDeleteResult(true);

        var result = await DeleteAsAdmin(user.Id.ToString());

        Assert.True(result.Success);
    }

    [Fact]
    public async Task DeleteAsync_SuperAdminDeletingAnAdmin_IsAllowed()
    {
        var user = BuildUser("adm");
        SetupUserFound(user);
        SetupDeleteResult(true);

        var result = await _sut.DeleteAsync(user.Id.ToString(), RoleLevel.SuperAdmin, "super-1");

        Assert.True(result.Success);
    }

    // ---------------------------------------------------------------
    // Limpeza das credenciais
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_RevokesTheUsersActiveRefreshTokens()
    {
        // Antes eles ficavam ativos até expirar, e só não funcionavam porque RefreshTokenUseCase
        // procura o usuário antes de rotacionar — resolvia por acidente, não por desenho.
        var user = BuildUser();
        SetupUserFound(user);
        SetupDeleteResult(true);

        var active = RefreshToken.Create(user.Id, "h", "s", DateTime.UtcNow.AddDays(7));
        _refreshTokenRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { active });

        await DeleteAsAdmin(user.Id.ToString());

        Assert.False(active.IsActive);
        Assert.Equal("User deleted", active.ReasonRevoked);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheUsersValidationRecords()
    {
        var user = BuildUser();
        SetupUserFound(user);
        SetupDeleteResult(true);

        var validation = new Validation
        {
            AcessToken = "digest",
            UserId = user.Id.ToString(),
            UserEmail = user.Email
        };
        _validationRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<Expression<Func<Validation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { validation });

        await DeleteAsAdmin(user.Id.ToString());

        _validationRepositoryMock.Verify(
            r => r.Delete(validation.Id.ToString(), validation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenTheUserIsNotDeleted_DoesNotTouchCredentials()
    {
        var user = BuildUser();
        SetupUserFound(user);
        SetupDeleteResult(false);

        await DeleteAsAdmin(user.Id.ToString());

        _refreshTokenRepositoryMock.Verify(
            r => r.FindByFilter(
                It.IsAny<Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Usuário não encontrado
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_UserNotFound_ReturnsSuccessFalse()
    {
        _userRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<User>());

        var result = await DeleteAsAdmin(Guid.NewGuid().ToString());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task DeleteAsync_UserNotFound_ReturnsMessageUserNotFound()
    {
        _userRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<User>());

        var result = await DeleteAsAdmin(Guid.NewGuid().ToString());

        Assert.Equal("User not found", result.Message);
    }

    // ---------------------------------------------------------------
    // Repository.Delete devolve false
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_DeleteReturnsFalse_ReturnsSuccessFalse()
    {
        var user = BuildUser();
        SetupUserFound(user);
        SetupDeleteResult(false);

        var result = await DeleteAsAdmin(user.Id.ToString());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task DeleteAsync_DeleteReturnsFalse_ReturnsMessageUserNotDeleted()
    {
        var user = BuildUser();
        SetupUserFound(user);
        SetupDeleteResult(false);

        var result = await DeleteAsAdmin(user.Id.ToString());

        Assert.Equal("User not deleted", result.Message);
    }

    // ---------------------------------------------------------------
    // Exceção
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_RepositoryThrows_ReturnsSuccessFalse()
    {
        _userRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection dropped"));

        var result = await DeleteAsAdmin(Guid.NewGuid().ToString());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task DeleteAsync_RepositoryThrows_DoesNotPropagateException()
    {
        _userRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection dropped"));

        var exception = await Record.ExceptionAsync(() => DeleteAsAdmin(Guid.NewGuid().ToString()));

        Assert.Null(exception);
    }
}
