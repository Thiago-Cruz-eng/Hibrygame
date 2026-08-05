using MongoDB.Driver;

namespace Orchestrator.Infra.Mongo;

/// <summary>
/// Implementação de <see cref="IMongoDbContext"/>: um invólucro fino em volta do
/// <c>IMongoDatabase</c> do driver.
///
/// <para>
/// Não tem lógica nenhuma de propósito. Quem escolhe a connection string e o nome do banco é a
/// composição em <c>Program.cs</c>; esta classe só carrega o resultado dessa escolha.
/// </para>
/// </summary>
public class MongoDbContext : IMongoDbContext
{
    /// <inheritdoc />
    public IMongoDatabase Database { get; }

    public MongoDbContext(IMongoDatabase database)
    {
        Database = database;
    }
}
