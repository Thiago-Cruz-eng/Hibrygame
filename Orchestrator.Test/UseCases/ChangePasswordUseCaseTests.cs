using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Moq;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Interfaces;
using Xunit;

namespace Orchestrator.Test.UseCases;

/// <summary>
/// Troca de senha.
///
/// <para>
/// O caso de uso mudou em três pontos de segurança, e é isso que os testes novos guardam: o
/// usuário alvo vem por parâmetro (o controller o tira do claim <c>sub</c>) e não do corpo; a
/// gravação é parcial, por <c>IUserRepositoryNoSql.UpdatePassword</c>, e não pelo CRUD genérico;
/// e uma troca bem-sucedida revoga os refresh tokens ativos do usuário.
/// </para>
/// </summary>
public class ChangePasswordUseCaseTests
{
    private readonly Mock<IUserRepositoryNoSql> _userRepositoryMock;
    private readonly Mock<IRefreshTokenRepositoryNoSql> _refreshTokenRepositoryMock;
    private readonly Mock<ISecureHashingService> _hashingServiceMock;
    private readonly Mock<ILogger<ChangePasswordUseCase>> _loggerMock;
    private readonly ChangePasswordUseCase _sut;

    public ChangePasswordUseCaseTests()
    {
        _userRepositoryMock = new Mock<IUserRepositoryNoSql>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepositoryNoSql>();
        _hashingServiceMock = new Mock<ISecureHashingService>();
        _loggerMock = new Mock<ILogger<ChangePasswordUseCase>>();

        // Sem tokens, salvo quando o teste disser o contrário.
        _refreshTokenRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RefreshToken>());

        _refreshTokenRepositoryMock
            .Setup(r => r.Update(It.IsAny<string>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _sut = new ChangePasswordUseCase(
            _userRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _hashingServiceMock.Object,
            _loggerMock.Object);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static User BuildUser(string passwordHash = "old-hash", string salt = "old-salt")
        => User.Create("Test User", "test@example.com", "jogador", passwordHash, salt, new List<UserAssignment>(), "admin");

    private static ChangePasswordRequest BuildRequest(
        string currentPassword = "OldPass123",
        string newPassword = "NewPass456")
        => new()
        {
            // Os dois campos abaixo são ignorados pelo caso de uso — continuam no DTO só por
            // compatibilidade de contrato. Preenchidos com lixo de propósito.
            UserId = "id-forjado-pelo-cliente",
            ModifiedBy = "autor-forjado-pelo-cliente",
            CurrentPassword = currentPassword,
            NewPassword = newPassword,
            NewPasswordConfirmation = newPassword
        };

    private void SetupUserFound(User user)
        => _userRepositoryMock
            .Setup(r => r.GetById(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

    private void SetupHappyPath(User user)
    {
        SetupUserFound(user);
        _hashingServiceMock
            .Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        _hashingServiceMock.Setup(h => h.HashValue(It.IsAny<string>())).Returns(("new-hash", "new-salt"));
        _userRepositoryMock
            .Setup(r => r.UpdatePassword(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    // ---------------------------------------------------------------
    // Happy path
    // ---------------------------------------------------------------

    [Fact]
    public async Task ChangeAsync_ValidCredentials_ReturnsSuccessTrue()
    {
        var user = BuildUser();
        SetupHappyPath(user);

        var result = await _sut.ChangeAsync(user.Id.ToString(), BuildRequest());

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ChangeAsync_ValidCredentials_ReturnsMessagePasswordUpdated()
    {
        var user = BuildUser();
        SetupHappyPath(user);

        var result = await _sut.ChangeAsync(user.Id.ToString(), BuildRequest());

        Assert.Equal("Password updated", result.Message);
    }

    [Fact]
    public async Task ChangeAsync_ValidCredentials_UpdatesOnlyThePasswordFields()
    {
        // Atualização parcial, e não substituição do documento: substituir desfaria qualquer
        // outro campo que tivesse mudado entre a leitura e a gravação.
        var user = BuildUser();
        SetupHappyPath(user);

        await _sut.ChangeAsync(user.Id.ToString(), BuildRequest());

        _userRepositoryMock.Verify(
            r => r.UpdatePassword(user, It.IsAny<CancellationToken>()), Times.Once);
        _userRepositoryMock.Verify(
            r => r.Update(It.IsAny<string>(), It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangeAsync_ValidCredentials_AuditsTheCallerFromTheToken_NotTheBody()
    {
        // A auditoria grava o id que o controller extraiu do claim `sub`. O ModifiedBy do corpo
        // era forjável e não é mais lido.
        var user = BuildUser();
        SetupHappyPath(user);

        await _sut.ChangeAsync(user.Id.ToString(), BuildRequest());

        Assert.NotNull(user.ModificationInformations);
        Assert.Equal(user.Id.ToString(), user.ModificationInformations!.ModifiedBy);
        Assert.NotEqual("autor-forjado-pelo-cliente", user.ModificationInformations.ModifiedBy);
    }

    [Fact]
    public async Task ChangeAsync_ValidCredentials_RevokesEveryActiveRefreshToken()
    {
        // É isto que faz "troquei a senha" significar "as outras sessões morreram". Sem isso, um
        // refresh token copiado continuaria renovando o acesso por até 30 dias.
        var user = BuildUser();
        SetupHappyPath(user);

        var active = RefreshToken.Create(user.Id, "h1", "s1", DateTime.UtcNow.AddDays(7));
        var alsoActive = RefreshToken.Create(user.Id, "h2", "s2", DateTime.UtcNow.AddDays(7));
        _refreshTokenRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { active, alsoActive });

        await _sut.ChangeAsync(user.Id.ToString(), BuildRequest());

        Assert.False(active.IsActive);
        Assert.False(alsoActive.IsActive);
        Assert.Equal("Password changed", active.ReasonRevoked);
        _refreshTokenRepositoryMock.Verify(
            r => r.Update(It.IsAny<string>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ChangeAsync_ValidCredentials_LeavesAlreadyRevokedTokensUntouched()
    {
        // Revogar de novo apagaria o motivo e a data originais — que são a evidência de por que
        // aquela sessão terminou.
        var user = BuildUser();
        SetupHappyPath(user);

        var revoked = RefreshToken.Create(user.Id, "h", "s", DateTime.UtcNow.AddDays(7));
        revoked.Revoke("Rotated");
        _refreshTokenRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { revoked });

        await _sut.ChangeAsync(user.Id.ToString(), BuildRequest());

        Assert.Equal("Rotated", revoked.ReasonRevoked);
        _refreshTokenRepositoryMock.Verify(
            r => r.Update(It.IsAny<string>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Usuário não encontrado
    // ---------------------------------------------------------------

    [Fact]
    public async Task ChangeAsync_UserNotFound_ReturnsSuccessFalse()
    {
        _userRepositoryMock
            .Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _sut.ChangeAsync(Guid.NewGuid().ToString(), BuildRequest());

        Assert.False(result.Success);
        Assert.Equal("User not found", result.Message);
    }

    [Fact]
    public async Task ChangeAsync_CallerIdIsNotAGuid_ReturnsUserNotFound()
    {
        // O id chega do claim `sub`, que é texto: id malformado é "não encontrado", não exceção.
        var result = await _sut.ChangeAsync("nao-e-guid", BuildRequest());

        Assert.False(result.Success);
        Assert.Equal("User not found", result.Message);
        _userRepositoryMock.Verify(
            r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------
    // Senha atual errada
    // ---------------------------------------------------------------

    [Fact]
    public async Task ChangeAsync_WrongCurrentPassword_ReturnsInvalidCredentials()
    {
        var user = BuildUser();
        SetupUserFound(user);
        _hashingServiceMock
            .Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        var result = await _sut.ChangeAsync(user.Id.ToString(), BuildRequest(currentPassword: "WrongPass!"));

        Assert.False(result.Success);
        Assert.Equal("Invalid credentials", result.Message);
    }

    [Fact]
    public async Task ChangeAsync_WrongCurrentPassword_DoesNotWriteAnything()
    {
        var user = BuildUser();
        SetupUserFound(user);
        _hashingServiceMock
            .Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        await _sut.ChangeAsync(user.Id.ToString(), BuildRequest(currentPassword: "WrongPass!"));

        _userRepositoryMock.Verify(
            r => r.UpdatePassword(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepositoryMock.Verify(
            r => r.Update(It.IsAny<string>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangeAsync_WrongCurrentPassword_LogsAWarning()
    {
        // Troca de senha com a senha atual errada é o que uma tentativa de tomada de conta
        // produz. Sem este registro, o incidente não deixa rastro nenhum.
        var user = BuildUser();
        SetupUserFound(user);
        _hashingServiceMock
            .Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        await _sut.ChangeAsync(user.Id.ToString(), BuildRequest(currentPassword: "WrongPass!"));

        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Exceção
    // ---------------------------------------------------------------

    [Fact]
    public async Task ChangeAsync_RepositoryThrows_ReturnsSuccessFalse()
    {
        _userRepositoryMock
            .Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Timeout"));

        var result = await _sut.ChangeAsync(Guid.NewGuid().ToString(), BuildRequest());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ChangeAsync_RepositoryThrows_DoesNotPropagateException()
    {
        _userRepositoryMock
            .Setup(r => r.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Timeout"));

        var exception = await Record.ExceptionAsync(
            () => _sut.ChangeAsync(Guid.NewGuid().ToString(), BuildRequest()));

        Assert.Null(exception);
    }
}
