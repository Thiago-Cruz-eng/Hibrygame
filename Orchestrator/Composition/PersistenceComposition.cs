using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Orchestrator.Infra.BaseRepository;
using Orchestrator.Infra.Interfaces;
using Orchestrator.Infra.Mongo;
using Orchestrator.Infra.Repositories;

namespace Orchestrator.Composition;

/// <summary>
/// Persistência: como os tipos do .NET são gravados no MongoDB, e quem fala com o banco.
/// </summary>
public static class PersistenceComposition
{
    /// <summary>
    /// Connection string usada quando a configuração não traz nenhuma.
    ///
    /// <para>
    /// <b>Hoje este fallback é sempre o que vale</b>, e não por escolha: o código lê
    /// <c>Mongo:ConnectionString</c> e <c>Mongo:Database</c>, mas o <c>appsettings.json</c> declara
    /// a seção como <c>HibrygameDatabase:*</c>. As chaves nunca casam, então a configuração é
    /// decorativa. É o DT-06 — ao corrigir, escolha <b>uma</b> das duas grafias e atualize
    /// <c>README.md</c>, <c>docs/ARCHITECTURE.md</c> e <c>.claude/CLAUDE.md</c> no mesmo PR.
    /// </para>
    /// </summary>
    private const string DefaultConnectionString = "mongodb://localhost:27017";

    /// <summary>Nome de banco usado quando a configuração não traz nenhum. Ver DT-06.</summary>
    private const string DefaultDatabaseName = "Hibrygame";

    /// <summary>
    /// Ensina o driver do MongoDB a gravar <see cref="Guid"/>, <see cref="DateTime"/> e
    /// <see cref="DateTimeOffset"/> como <b>texto</b>, e não no formato binário padrão.
    ///
    /// <para>
    /// A troca é por legibilidade: um documento com <c>_id</c> em texto pode ser lido, copiado e
    /// procurado à mão no banco, o que o formato binário do driver não permite.
    /// </para>
    ///
    /// <para>
    /// <b>Global e definitivo.</b> Vale para todos os tipos, não pode ser desfeito em tempo de
    /// execução e tem de ser feito <b>antes</b> da primeira operação com o banco — daí ser a
    /// primeira coisa do <c>Program.cs</c>. E é decisão de formato de dado: mudar isto depois de
    /// existirem documentos gravados os torna ilegíveis, porque o driver passaria a esperar outro
    /// formato.
    /// </para>
    /// </summary>
    public static void RegisterBsonSerializers()
    {
        BsonSerializer.RegisterSerializer(new GuidSerializer(BsonType.String));
        BsonSerializer.RegisterSerializer(new DateTimeSerializer(BsonType.String));
        BsonSerializer.RegisterSerializer(new DateTimeOffsetSerializer(BsonType.String));
    }

    /// <summary>
    /// Registra o cliente do Mongo, o contexto e os repositórios.
    /// </summary>
    public static IServiceCollection AddMongoPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Não é consumido por ninguém — ver a nota em IMongoDbContextFactory, que descreve a
        // decisão pendente. Mantido só para não remover às escondidas o que sinaliza uma intenção
        // inacabada.
        services.AddSingleton<IMongoDbContextFactory, MongoDbContextFactory>();

        // Singleton, e isto importa: o MongoClient administra internamente um pool de conexões e é
        // feito para viver o processo inteiro. Um por requisição esgotaria conexões no banco.
        services.AddSingleton<IMongoClient>(_ =>
        {
            var connection = configuration.GetSection("Mongo:ConnectionString").Value
                             ?? DefaultConnectionString;
            return new MongoClient(connection);
        });

        services.AddSingleton<IMongoDbContext>(serviceProvider =>
        {
            var client = serviceProvider.GetRequiredService<IMongoClient>();
            var databaseName = configuration.GetSection("Mongo:Database").Value ?? DefaultDatabaseName;
            return new MongoDbContext(client.GetDatabase(databaseName));
        });

        // Scoped: um por requisição. O repositório em si não guarda estado, mas scoped é o padrão
        // seguro para quem depende do contexto — se algum dia ele passar a acumular algo por
        // operação, o escopo já está certo.
        services.AddScoped<IGenericRepository, GenericRepository>();

        // Um par interface/classe por entidade. Repositório novo entra nesta lista — sem isso ele
        // existe mas a injeção falha em tempo de execução, não de compilação.
        services.AddScoped<IUserRepositoryNoSql, UserRepositoryNoSql>();
        services.AddScoped<IRefreshTokenRepositoryNoSql, RefreshTokenRepositoryNoSql>();
        services.AddScoped<IValidationRepositoryNoSql, ValidationRepositoryNoSql>();

        return services;
    }
}
