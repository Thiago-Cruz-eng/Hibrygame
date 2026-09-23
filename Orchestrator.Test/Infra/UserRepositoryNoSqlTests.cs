using System.Linq.Expressions;
using Moq;
using Orchestrator.Domain;
using Orchestrator.Infra.BaseRepository;
using Orchestrator.Infra.Repositories;
using Xunit;

namespace Orchestrator.Test.Infra;

/// <summary>
/// A única operação que <see cref="UserRepositoryNoSql"/> acrescenta ao CRUD da base:
/// <c>UpdatePassword</c>.
///
/// <para>
/// <b>Por que ela merece teste próprio.</b> É uma atualização <b>parcial</b> (<c>$set</c> em
/// quatro campos) e não uma substituição de documento — e a diferença não aparece em nenhum
/// resultado que o caso de uso possa observar. Se alguém trocar por <c>ReplaceOne</c>, todos os
/// testes de <c>ChangePasswordUseCase</c> continuam verdes e o defeito só aparece em produção,
/// quando uma alteração concorrente de nome ou papel for desfeita em silêncio. Este arquivo é o
/// que trava isso.
/// </para>
/// </summary>
public class UserRepositoryNoSqlTests
{
    private readonly Mock<IGenericRepository> _genericRepositoryMock = new();
    private readonly UserRepositoryNoSql _sut;

    public UserRepositoryNoSqlTests()
    {
        _sut = new UserRepositoryNoSql(_genericRepositoryMock.Object);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static User BuildUser()
        => User.Create("Test User", "test@example.com", "jogador", "old-hash", "old-salt", [], "admin");

    /// <summary>
    /// O nome da propriedade que um seletor de campo aponta.
    ///
    /// <para>
    /// O <c>Expression&lt;Func&lt;User, object&gt;&gt;</c> embrulha propriedades de tipo valor num
    /// <c>Convert</c> — é isso que o <c>UnaryExpression</c> abaixo desfaz. Sem desembrulhar, o
    /// seletor de <c>MustChangePassword</c> não seria reconhecido.
    /// </para>
    /// </summary>
    private static string FieldName(Expression<Func<User, object>> selector)
    {
        var body = selector.Body is UnaryExpression unary ? unary.Operand : selector.Body;
        return ((MemberExpression)body).Member.Name;
    }

    /// <summary>Filtro que o repositório mandou ao <c>$set</c>. Preenchido por <see cref="CaptureUpdate"/>.</summary>
    private Expression<Func<User, bool>>? _capturedFilter;

    /// <summary>Pares (campo, valor) que o repositório mandou ao <c>$set</c>.</summary>
    private (Expression<Func<User, object>> Field, object Value)[]? _capturedUpdates;

    /// <summary>
    /// Arma o dublê para guardar o que <c>UpdatePassword</c> enviar.
    ///
    /// <para>
    /// Os valores só existem <b>depois</b> da chamada, por isso ficam em campo e não no retorno:
    /// devolver a tupla aqui congelaria dois <c>null</c>.
    /// </para>
    /// </summary>
    private void CaptureUpdate()
    {
        _genericRepositoryMock
            .Setup(r => r.Update(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<(Expression<Func<User, object>>, object)[]>()))
            .Callback((
                Expression<Func<User, bool>> filter,
                CancellationToken _,
                (Expression<Func<User, object>>, object)[] updates) =>
            {
                _capturedFilter = filter;
                _capturedUpdates = updates;
            })
            .ReturnsAsync(true);
    }

    // ---------------------------------------------------------------
    // UpdatePassword — o $set direcionado
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdatePassword_UpdatesOnlyTheFourPasswordFields()
    {
        var user = BuildUser();
        CaptureUpdate();

        user.ChangePassword("new-hash", "new-salt", "quem-trocou");
        await _sut.UpdatePassword(user);

        Assert.NotNull(_capturedUpdates);
        var fields = _capturedUpdates!.Select(u => FieldName(u.Field)).ToArray();

        // Exatamente estes quatro. Um campo a mais aqui é um campo que a troca de senha passa a
        // sobrescrever sem ninguém ter pedido.
        Assert.Equal(
            new[] { "PasswordHash", "Salt", "MustChangePassword", "ModificationInformations" },
            fields);
    }

    [Fact]
    public async Task UpdatePassword_SendsTheValuesFromTheMutatedEntity()
    {
        var user = BuildUser();
        CaptureUpdate();

        user.ChangePassword("new-hash", "new-salt", "quem-trocou");
        await _sut.UpdatePassword(user);

        Assert.NotNull(_capturedUpdates);
        var byField = _capturedUpdates!.ToDictionary(u => FieldName(u.Field), u => u.Value);

        Assert.Equal("new-hash", byField["PasswordHash"]);
        Assert.Equal("new-salt", byField["Salt"]);
        Assert.Equal(false, byField["MustChangePassword"]);
        Assert.Same(user.ModificationInformations, byField["ModificationInformations"]);
    }

    [Fact]
    public async Task UpdatePassword_FiltersByTheUsersId()
    {
        var user = BuildUser();
        var other = BuildUser();
        CaptureUpdate();

        user.ChangePassword("new-hash", "new-salt", "quem-trocou");
        await _sut.UpdatePassword(user);

        Assert.NotNull(_capturedFilter);
        var predicate = _capturedFilter!.Compile();

        Assert.True(predicate(user));
        Assert.False(predicate(other));
    }

    [Fact]
    public async Task UpdatePassword_NeverReplacesTheWholeDocument()
    {
        // ReplaceOne desfaria qualquer campo alterado entre a leitura e a gravação.
        var user = BuildUser();
        CaptureUpdate();

        user.ChangePassword("new-hash", "new-salt", "quem-trocou");
        await _sut.UpdatePassword(user);

        _genericRepositoryMock.Verify(
            r => r.ReplaceOne(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdatePassword_PropagatesTheRepositoryResult()
    {
        var user = BuildUser();
        user.ChangePassword("new-hash", "new-salt", "quem-trocou");

        _genericRepositoryMock
            .Setup(r => r.Update(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<(Expression<Func<User, object>>, object)[]>()))
            .ReturnsAsync(false);

        // false significa "nada casou com o id OU os valores já eram os gravados" — ver a nota de
        // retorno em IGenericRepository.Update. O repositório não interpreta, só repassa.
        Assert.False(await _sut.UpdatePassword(user));
    }

    // ---------------------------------------------------------------
    // A guarda de auditoria
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdatePassword_OnAUserThatWasNeverMutated_Throws()
    {
        // Usuário recém-criado não tem ModificationInformations. Gravar assim escreveria null no
        // campo de auditoria; falhar aqui diz exatamente o que faltou fazer.
        var user = BuildUser();

        var exception = await Record.ExceptionAsync(() => _sut.UpdatePassword(user));

        var invalid = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("ChangePassword", invalid.Message);
    }

    [Fact]
    public async Task UpdatePassword_OnAUserThatWasNeverMutated_TouchesTheDatabaseNotAtAll()
    {
        var user = BuildUser();

        await Record.ExceptionAsync(() => _sut.UpdatePassword(user));

        _genericRepositoryMock.Verify(
            r => r.Update(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<(Expression<Func<User, object>>, object)[]>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // O CRUD herdado continua chegando ao repositório genérico
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetById_DelegatesToTheGenericRepository()
    {
        var user = BuildUser();
        _genericRepositoryMock
            .Setup(r => r.GetFirstOrDefault(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        Assert.Same(user, await _sut.GetById(user.Id));
    }
}
