using System.Reflection;
using MongoDB.Driver;
using Orchestrator.Domain;
using Orchestrator.Infra.Utils;

namespace Orchestrator.Infra.Mongo;

/// <summary>
/// Cria, na subida da aplicação, os índices que as consultas quentes precisam — e o índice único
/// que a unicidade de e-mail sempre dependeu e nunca teve (era o DT-18).
///
/// <para>
/// <b>Por que o índice único importa mais que velocidade.</b> A unicidade de e-mail era garantida
/// só em código (<c>FindByFilter</c> seguido de <c>if</c>), e entre a consulta e a gravação existe
/// uma janela: dois cadastros simultâneos com o mesmo e-mail passam os dois. A partir daí o login
/// (<c>FirstOrDefault</c> por e-mail) escolhe um dos dois registros por ordem de retorno do banco,
/// e o usuário "às vezes" entra com a senha certa. O índice único fecha a janela no único lugar
/// onde ela pode ser fechada: o banco.
/// </para>
///
/// <para>
/// <b>Idempotente.</b> <c>CreateOne</c> sobre um índice que já existe, com a mesma definição, não
/// faz nada. Pode rodar em toda subida.
/// </para>
///
/// <para>
/// <b>Nunca derruba a aplicação.</b> Falha aqui vira <c>LogError</c> e segue. O caso previsto é
/// concreto: se a coleção já contiver e-mails duplicados — resultado justamente da corrida que
/// este índice fecha —, o Mongo recusa criar o índice único. Derrubar a subida deixaria a API
/// fora do ar por um dado antigo; o log diz o que fazer.
/// </para>
/// </summary>
public class MongoIndexInitializer : IHostedService
{
    private readonly IMongoDbContext _context;
    private readonly ILogger<MongoIndexInitializer> _logger;

    public MongoIndexInitializer(IMongoDbContext context, ILogger<MongoIndexInitializer> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Cria os três índices. Cada um em seu próprio <c>try</c>: a falha de um não pode impedir os
    /// outros — o único que costuma falhar é o único, e os outros dois é que aceleram consulta.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await CreateIndexAsync<User>(
            Builders<User>.IndexKeys.Ascending(user => user.Email),
            new CreateIndexOptions { Name = "ux_user_email", Unique = true },
            "Índice único de User.Email não pôde ser criado. A causa mais provável é já existir " +
            "e-mail duplicado na coleção: liste os duplicados e resolva antes de tentar de novo. " +
            "Até lá a unicidade continua garantida apenas em código, com a janela de corrida " +
            "descrita em MongoIndexInitializer.",
            cancellationToken);

        await CreateIndexAsync<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(token => token.UserId),
            new CreateIndexOptions { Name = "ix_refreshtoken_userid" },
            "Índice de RefreshToken.UserId não pôde ser criado. Sem ele, cada refresh varre a " +
            "coleção inteira.",
            cancellationToken);

        await CreateIndexAsync<Validation>(
            Builders<Validation>.IndexKeys.Ascending(validation => validation.UserId),
            new CreateIndexOptions { Name = "ix_validation_userid" },
            "Índice de Validation.UserId não pôde ser criado. Sem ele, cada chamada de " +
            "/validation varre a coleção inteira.",
            cancellationToken);
    }

    /// <summary>Nada a desfazer: índice é estado do banco, não do processo.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task CreateIndexAsync<T>(
        IndexKeysDefinition<T> keys,
        CreateIndexOptions options,
        string failureAdvice,
        CancellationToken cancellationToken) where T : BaseEntity
    {
        var collectionName = ResolveCollectionName<T>();

        try
        {
            var collection = _context.Database.GetCollection<T>(collectionName);
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<T>(keys, options), cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Índice {IndexName} garantido na coleção {Collection}.", options.Name, collectionName);
        }
        // Dois tipos, e apenas dois. `MongoException` é a raiz de toda a família do driver —
        // `MongoCommandException` (o servidor recusou criar o índice, que é o caso do e-mail
        // duplicado), `MongoAuthenticationException`, `MongoConnectionException`. `TimeoutException`
        // é o que a seleção de servidor estoura quando não há banco no endereço configurado, e ela
        // NÃO herda de MongoException.
        //
        // Um `catch (Exception)` aqui engoliria também o que é defeito de programação —
        // NullReference num campo novo, InvalidOperation numa definição de índice mal montada — e
        // esses têm de subir e derrubar a subida, porque são erro nosso e não do ambiente.
        catch (Exception e) when (e is MongoException or TimeoutException)
        {
            _logger.LogError(e,
                "Falha ao criar o índice {IndexName} na coleção {Collection}. {Advice}",
                options.Name, collectionName, failureAdvice);
        }
    }

    /// <summary>
    /// Mesma regra de <c>GenericRepository</c>: o nome vem de <c>[CollectionName]</c>, com o nome
    /// do tipo como reserva. Duplicar a regra seria arriscar índice criado numa coleção e dados
    /// gravados em outra.
    /// </summary>
    private static string ResolveCollectionName<T>() where T : BaseEntity =>
        typeof(T).GetCustomAttribute<CollectionNameAttribute>()?.CollectionName ?? typeof(T).Name;
}
