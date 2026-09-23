using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using Orchestrator.Domain;
using Orchestrator.Infra.Mongo;
using Xunit;

namespace Orchestrator.Test.Infra;

/// <summary>
/// Criação dos índices na subida.
///
/// <para>
/// <b>Nada aqui abre conexão.</b> O <c>IMongoDbContext</c>, o banco, as coleções e o gerenciador de
/// índices são todos dublês do Moq — o que se verifica é o que o inicializador <b>pede</b> ao
/// driver, não o que o MongoDB faz com o pedido.
/// </para>
///
/// <para>
/// <b>O teste que mais importa é o do caminho de falha.</b> O índice único de <c>User.Email</c>
/// falha quando a coleção já tem e-mail duplicado — que é exatamente a situação que ele existe para
/// impedir daqui em diante. Se essa falha derrubasse a subida, a aplicação ficaria fora do ar por
/// causa de um dado antigo, e o remédio seria pior que a doença.
/// </para>
/// </summary>
public class MongoIndexInitializerTests
{
    private readonly Mock<IMongoDatabase> _databaseMock = new();
    private readonly Mock<ILogger<MongoIndexInitializer>> _loggerMock = new();
    private readonly MongoIndexInitializer _sut;

    /// <summary>Um gerenciador de índices por coleção, para verificar cada uma isoladamente.</summary>
    private readonly Mock<IMongoIndexManager<User>> _userIndexes = new();
    private readonly Mock<IMongoIndexManager<RefreshToken>> _refreshTokenIndexes = new();
    private readonly Mock<IMongoIndexManager<Validation>> _validationIndexes = new();

    public MongoIndexInitializerTests()
    {
        var contextMock = new Mock<IMongoDbContext>();
        contextMock.Setup(c => c.Database).Returns(_databaseMock.Object);

        WireCollection(_userIndexes);
        WireCollection(_refreshTokenIndexes);
        WireCollection(_validationIndexes);

        _sut = new MongoIndexInitializer(contextMock.Object, _loggerMock.Object);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Liga <c>Database.GetCollection&lt;T&gt;(qualquer nome)</c> a uma coleção dublê cujo
    /// <c>Indexes</c> é o gerenciador informado.
    /// </summary>
    private void WireCollection<T>(Mock<IMongoIndexManager<T>> indexes)
    {
        var collection = new Mock<IMongoCollection<T>>();
        collection.Setup(c => c.Indexes).Returns(indexes.Object);

        indexes
            .Setup(i => i.CreateOneAsync(
                It.IsAny<CreateIndexModel<T>>(),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<CreateIndexModel<T>, CreateOneIndexOptions, CancellationToken>(
                (model, _, _) => _capturedModels[typeof(T)] = model)
            .ReturnsAsync("index-name");

        _databaseMock
            .Setup(d => d.GetCollection<T>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
            .Returns(collection.Object);
    }

    /// <summary>
    /// O modelo de índice enviado ao gerenciador de cada entidade, guardado por
    /// <see cref="WireCollection{T}"/> no momento da chamada.
    /// </summary>
    private readonly Dictionary<Type, object> _capturedModels = new();

    /// <summary>O modelo de índice que foi enviado para <typeparamref name="T"/>.</summary>
    private CreateIndexModel<T> CapturedModel<T>()
    {
        Assert.True(_capturedModels.ContainsKey(typeof(T)),
            $"Nenhum indice foi pedido para {typeof(T).Name}.");

        return (CreateIndexModel<T>)_capturedModels[typeof(T)];
    }

    /// <summary>
    /// As chaves do índice, como documento BSON — <c>{ "Email" : 1 }</c> para um ascendente simples.
    ///
    /// <para>
    /// <c>IndexKeysDefinition</c> não expõe os campos diretamente: é preciso renderizá-lo com o
    /// serializador da entidade, que é o mesmo caminho que o driver percorre ao mandar o comando.
    /// </para>
    /// </summary>
    private static BsonDocument RenderKeys<T>(CreateIndexModel<T> model) =>
        model.Keys.Render(new RenderArgs<T>(
            BsonSerializer.SerializerRegistry.GetSerializer<T>(),
            BsonSerializer.SerializerRegistry));

    // ---------------------------------------------------------------
    // As três coleções são indexadas
    // ---------------------------------------------------------------

    [Fact]
    public async Task StartAsync_CreatesOneIndexOnEachCollection()
    {
        await _sut.StartAsync(CancellationToken.None);

        _userIndexes.Verify(
            i => i.CreateOneAsync(
                It.IsAny<CreateIndexModel<User>>(),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _refreshTokenIndexes.Verify(
            i => i.CreateOneAsync(
                It.IsAny<CreateIndexModel<RefreshToken>>(),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _validationIndexes.Verify(
            i => i.CreateOneAsync(
                It.IsAny<CreateIndexModel<Validation>>(),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("User")]
    [InlineData("RefreshToken")]
    [InlineData("Validation")]
    public async Task StartAsync_ResolvesTheCollectionNameFromTheAttribute(string collectionName)
    {
        // Mesma regra de GenericRepository: o nome vem de [CollectionName]. Duplicar a regra com
        // uma string literal seria arriscar índice criado numa coleção e dados gravados em outra.
        await _sut.StartAsync(CancellationToken.None);

        _databaseMock.Verify(
            d => d.GetCollection<It.IsAnyType>(collectionName, It.IsAny<MongoCollectionSettings>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // A forma de cada índice
    // ---------------------------------------------------------------

    [Fact]
    public async Task StartAsync_TheEmailIndexIsUnique()
    {
        // É a única coisa que fecha a janela de corrida entre o "já existe este e-mail?" e o Save.
        // Sem Unique, o índice só acelera a consulta e a duplicata continua entrando.
        await _sut.StartAsync(CancellationToken.None);

        var model = CapturedModel<User>();

        Assert.True(model.Options.Unique);
        Assert.Equal("ux_user_email", model.Options.Name);
        Assert.Equal(new BsonDocument("Email", 1), RenderKeys(model));
    }

    [Fact]
    public async Task StartAsync_TheRefreshTokenIndexIsOnUserId_AndIsNotUnique()
    {
        // Não pode ser único: um usuário tem muitos refresh tokens, e os revogados continuam lá.
        await _sut.StartAsync(CancellationToken.None);

        var model = CapturedModel<RefreshToken>();

        Assert.NotEqual(true, model.Options.Unique);
        Assert.Equal("ix_refreshtoken_userid", model.Options.Name);
        Assert.Equal(new BsonDocument("UserId", 1), RenderKeys(model));
    }

    [Fact]
    public async Task StartAsync_TheValidationIndexIsOnUserId_AndIsNotUnique()
    {
        await _sut.StartAsync(CancellationToken.None);

        var model = CapturedModel<Validation>();

        Assert.NotEqual(true, model.Options.Unique);
        Assert.Equal("ix_validation_userid", model.Options.Name);
        Assert.Equal(new BsonDocument("UserId", 1), RenderKeys(model));
    }

    // ---------------------------------------------------------------
    // Falha não derruba a subida
    // ---------------------------------------------------------------

    /// <summary>
    /// Faz o índice de <see cref="User"/> falhar com <paramref name="failure"/>.
    /// </summary>
    private void FailUserIndex(Exception failure)
        => _userIndexes
            .Setup(i => i.CreateOneAsync(
                It.IsAny<CreateIndexModel<User>>(),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

    /// <summary>
    /// Uma falha do driver, construída sem servidor. <c>MongoException</c> é a raiz da família que
    /// o <c>catch</c> declara.
    /// </summary>
    private static MongoException DriverFailure() =>
        new MongoException("E11000 duplicate key error collection: Hibrygame.User index: ux_user_email");

    [Fact]
    public async Task StartAsync_WhenTheUniqueIndexFails_DoesNotThrow()
    {
        // O caso previsto e concreto: a coleção já tem e-mail duplicado e o Mongo recusa criar o
        // índice. Derrubar a subida deixaria a API fora do ar por causa de um dado antigo.
        FailUserIndex(DriverFailure());

        Assert.Null(await Record.ExceptionAsync(() => _sut.StartAsync(CancellationToken.None)));
    }

    [Fact]
    public async Task StartAsync_WhenAServerCannotBeSelected_DoesNotThrow()
    {
        // TimeoutException é o que a seleção de servidor estoura quando não há banco no endereço
        // configurado, e ela NÃO herda de MongoException — por isso está no catch por nome.
        FailUserIndex(new TimeoutException("A timeout occurred after 30000ms selecting a server."));

        Assert.Null(await Record.ExceptionAsync(() => _sut.StartAsync(CancellationToken.None)));
    }

    [Fact]
    public async Task StartAsync_WhenTheUniqueIndexFails_LogsAnErrorWithTheAdvice()
    {
        FailUserIndex(DriverFailure());

        await _sut.StartAsync(CancellationToken.None);

        // Sem o log, a aplicação sobe sem o índice e ninguém fica sabendo. A mensagem tem de dizer
        // o que fazer — por isso a asserção é sobre o conteúdo, não só sobre o nível.
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("e-mail duplicado")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WhenOneIndexFails_TheOthersAreStillCreated()
    {
        // Cada índice no seu próprio try: o único costuma ser o que falha, e os outros dois é que
        // aceleram consulta. Um não pode levar os outros junto.
        FailUserIndex(DriverFailure());

        await _sut.StartAsync(CancellationToken.None);

        _refreshTokenIndexes.Verify(
            i => i.CreateOneAsync(
                It.IsAny<CreateIndexModel<RefreshToken>>(),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _validationIndexes.Verify(
            i => i.CreateOneAsync(
                It.IsAny<CreateIndexModel<Validation>>(),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_ADefectInOurOwnCode_IsNotSwallowed()
    {
        // O catch é estreito de propósito. NullReference ou InvalidOperation aqui é erro NOSSO —
        // campo novo mal referenciado, definição de índice mal montada — e tem de derrubar a
        // subida em vez de virar uma linha de log que ninguém lê. Alargar este catch para
        // `Exception` faz este teste falhar, e é para isso que ele existe.
        FailUserIndex(new InvalidOperationException("definição de índice mal montada"));

        var exception = await Record.ExceptionAsync(() => _sut.StartAsync(CancellationToken.None));

        Assert.IsType<InvalidOperationException>(exception);
    }

    // ---------------------------------------------------------------
    // Sucesso também é registrado, e parar não faz nada
    // ---------------------------------------------------------------

    [Fact]
    public async Task StartAsync_LogsOneInformationPerIndexCreated()
    {
        await _sut.StartAsync(CancellationToken.None);

        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task StopAsync_DoesNothing()
    {
        // Índice é estado do banco, não do processo: não há o que desfazer ao desligar.
        await _sut.StopAsync(CancellationToken.None);

        _databaseMock.VerifyNoOtherCalls();
    }
}
