using Microsoft.Extensions.Logging;
using Moq;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Security;
using Xunit;

namespace Orchestrator.Test.UseCases;

public class ValidationServiceTests
{
    private readonly Mock<IValidationRepositoryNoSql> _validationRepositoryMock;
    private readonly Mock<ILogger<ValidationService>> _loggerMock;
    private readonly ValidationService _sut;

    public ValidationServiceTests()
    {
        _validationRepositoryMock = new Mock<IValidationRepositoryNoSql>();
        _loggerMock = new Mock<ILogger<ValidationService>>();
        _sut = new ValidationService(_validationRepositoryMock.Object, _loggerMock.Object);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static ValidationDto BuildDto(
        string accessToken = "tok-123",
        string userId = "user-1",
        string userEmail = "user@example.com",
        string? room = null,
        string? pieceColor = null)
        => new()
        {
            AcessToken = accessToken,
            UserId = userId,
            UserEmail = userEmail,
            Room = room,
            PieceColor = pieceColor
        };

    private static Validation BuildValidation(
        string accessToken = "tok-123",
        string userId = "user-1",
        string userEmail = "user@example.com",
        string? room = "room-1",
        string? pieceColor = "White")
        => new()
        {
            AcessToken = accessToken,
            UserId = userId,
            UserEmail = userEmail,
            Room = room,
            PieceColor = pieceColor
        };

    // ---------------------------------------------------------------
    // CreateValidation — happy path
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateValidation_ValidDto_ReturnsTrueAndCallsSave()
    {
        // Arrange
        _validationRepositoryMock
            .Setup(r => r.Save(It.IsAny<Validation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CreateValidation(BuildDto());

        // Assert
        Assert.True(result);
        _validationRepositoryMock.Verify(r => r.Save(It.IsAny<Validation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateValidation_ValidDto_StoresTheTokenDigest_NotTheToken()
    {
        // Era a DT-07: o access token ia para a coleção em claro, e quem lesse a coleção saía com
        // sessões prontas para usar. Agora vai o resumo SHA-256.
        Validation? savedValidation = null;
        _validationRepositoryMock
            .Setup(r => r.Save(It.IsAny<Validation>(), It.IsAny<CancellationToken>()))
            .Callback<Validation, CancellationToken>((v, _) => savedValidation = v)
            .Returns(Task.CompletedTask);

        await _sut.CreateValidation(BuildDto(accessToken: "my-token"));

        Assert.NotNull(savedValidation);
        Assert.NotEqual("my-token", savedValidation!.AcessToken);
        Assert.Equal(TokenDigest.Compute("my-token"), savedValidation.AcessToken);
    }

    [Fact]
    public async Task CreateValidation_TheDigestIsDeterministic()
    {
        // Precisa ser: o filtro do Mongo compara por igualdade, então o mesmo token tem de
        // produzir sempre o mesmo texto. É a diferença em relação ao PBKDF2 com salt.
        var digests = new List<string>();
        _validationRepositoryMock
            .Setup(r => r.Save(It.IsAny<Validation>(), It.IsAny<CancellationToken>()))
            .Callback<Validation, CancellationToken>((v, _) => digests.Add(v.AcessToken))
            .Returns(Task.CompletedTask);

        await _sut.CreateValidation(BuildDto(accessToken: "mesmo-token"));
        await _sut.CreateValidation(BuildDto(accessToken: "mesmo-token"));

        Assert.Equal(digests[0], digests[1]);
    }

    // ---------------------------------------------------------------
    // CreateValidation — exception returns false
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateValidation_RepositoryThrows_ReturnsFalse()
    {
        // Arrange
        _validationRepositoryMock
            .Setup(r => r.Save(It.IsAny<Validation>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Mongo error"));

        // Act
        var result = await _sut.CreateValidation(BuildDto());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CreateValidation_RepositoryThrows_DoesNotPropagateException()
    {
        // Arrange
        _validationRepositoryMock
            .Setup(r => r.Save(It.IsAny<Validation>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Mongo error"));

        // Act
        var exception = await Record.ExceptionAsync(() => _sut.CreateValidation(BuildDto()));

        // Assert
        Assert.Null(exception);
    }

    // ---------------------------------------------------------------
    // GetValidationByUserToken — returns existing validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetValidationByUserToken_ValidationExists_ReturnsValidation()
    {
        // Arrange
        var validation = BuildValidation(accessToken: "tok-abc", userId: "user-1");
        _validationRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<Validation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { validation });

        // Act
        var result = await _sut.GetValidationByUserToken("user-1", "tok-abc");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("tok-abc", result.AcessToken);
        Assert.Equal("user-1", result.UserId);
    }

    // ---------------------------------------------------------------
    // GetValidationByUserToken — throws when not found
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetValidationByUserToken_ValidationNotFound_ThrowsException()
    {
        // Arrange
        _validationRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<Validation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Validation>());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GetValidationByUserToken("user-1", "non-existent-token"));
    }

    // ---------------------------------------------------------------
    // UpdateValidationByUserToken — happy path
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateValidationByUserToken_ValidationExists_ReturnsTrueAndUpdatesRoomAndPieceColor()
    {
        // Arrange
        var validation = BuildValidation(accessToken: "tok-123", userId: "user-1", room: "old-room", pieceColor: "White");
        _validationRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<Validation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { validation });

        Validation? updatedValidation = null;
        _validationRepositoryMock
            .Setup(r => r.Update(It.IsAny<string>(), It.IsAny<Validation>(), It.IsAny<CancellationToken>()))
            .Callback<string, Validation, CancellationToken>((_, v, _) => updatedValidation = v)
            .ReturnsAsync(true);

        // Act
        var result = await _sut.UpdateValidationByUserToken("user-1", "tok-123", "Black", "new-room");

        // Assert
        Assert.True(result);
        Assert.NotNull(updatedValidation);
        Assert.Equal("new-room", updatedValidation!.Room);
        Assert.Equal("Black", updatedValidation.PieceColor);
    }

    [Fact]
    public async Task UpdateValidationByUserToken_ValidationExists_CallsUpdateOnRepository()
    {
        // Arrange
        var validation = BuildValidation(accessToken: "tok-123", userId: "user-1");
        _validationRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<Validation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { validation });
        _validationRepositoryMock
            .Setup(r => r.Update(It.IsAny<string>(), It.IsAny<Validation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.UpdateValidationByUserToken("user-1", "tok-123", "White", "room-1");

        // Assert
        _validationRepositoryMock.Verify(r => r.Update(validation.Id.ToString(), It.IsAny<Validation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------
    // UpdateValidationByUserToken — validation not found returns false
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateValidationByUserToken_ValidationNotFound_ReturnsFalse()
    {
        // Arrange
        _validationRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<Validation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Validation>());

        // Act
        var result = await _sut.UpdateValidationByUserToken("user-1", "bad-token", "White", "room-1");

        // Assert
        Assert.False(result);
    }

    // ---------------------------------------------------------------
    // GetValidationCanMove (6-param) — returns true when match exists
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetValidationCanMove_6Params_MatchingValidationExists_ReturnsTrue()
    {
        // Arrange
        var validation = BuildValidation(
            accessToken: "tok-abc",
            userId: "user-1",
            room: "room-1",
            pieceColor: "White");

        _validationRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<Validation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { validation });

        // Act
        var result = await _sut.GetValidationCanMove("user-1", "tok-abc", "White", "room-1");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetValidationCanMove_6Params_NoMatchingValidation_ReturnsFalse()
    {
        // Arrange
        _validationRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<Validation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Validation>());

        // Act
        var result = await _sut.GetValidationCanMove("user-1", "tok-abc", "White", "room-1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetValidationCanMove_6Params_RepositoryThrows_ReturnsFalse()
    {
        // Arrange
        _validationRepositoryMock
            .Setup(r => r.FindByFilter(It.IsAny<System.Linq.Expressions.Expression<Func<Validation, bool>>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        // Act
        var result = await _sut.GetValidationCanMove("user-1", "tok-abc", "White", "room-1");

        // Assert
        Assert.False(result);
    }

    // ---------------------------------------------------------------
    // O resumo do token entra no filtro, nunca o token
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetValidationByUserToken_FiltersByTheDigest_NotTheRawToken()
    {
        System.Linq.Expressions.Expression<Func<Validation, bool>>? captured = null;
        _validationRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<System.Linq.Expressions.Expression<Func<Validation, bool>>>(),
                It.IsAny<CancellationToken>()))
            .Callback<System.Linq.Expressions.Expression<Func<Validation, bool>>, CancellationToken>(
                (filter, _) => captured = filter)
            .ReturnsAsync(new[] { BuildValidation(accessToken: TokenDigest.Compute("tok-abc")) });

        await _sut.GetValidationByUserToken("user-1", "tok-abc");

        // O predicado capturado é executado em memória contra dois registros: só o que guarda o
        // RESUMO casa. Um registro com o token em claro — como os gravados antes desta mudança —
        // não casa mais, e é essa a quebra de compatibilidade documentada.
        Assert.NotNull(captured);
        var predicate = captured!.Compile();

        Assert.True(predicate(BuildValidation(accessToken: TokenDigest.Compute("tok-abc"))));
        Assert.False(predicate(BuildValidation(accessToken: "tok-abc")));
    }

    [Fact]
    public async Task GetValidationCanMove_FiltersByTheDigest_NotTheRawToken()
    {
        System.Linq.Expressions.Expression<Func<Validation, bool>>? captured = null;
        _validationRepositoryMock
            .Setup(r => r.FindByFilter(
                It.IsAny<System.Linq.Expressions.Expression<Func<Validation, bool>>>(),
                It.IsAny<CancellationToken>()))
            .Callback<System.Linq.Expressions.Expression<Func<Validation, bool>>, CancellationToken>(
                (filter, _) => captured = filter)
            .ReturnsAsync(Array.Empty<Validation>());

        await _sut.GetValidationCanMove("user-1", "tok-abc", "White", "room-1");

        Assert.NotNull(captured);
        var predicate = captured!.Compile();

        Assert.True(predicate(BuildValidation(accessToken: TokenDigest.Compute("tok-abc"))));
        Assert.False(predicate(BuildValidation(accessToken: "tok-abc")));
    }
}
