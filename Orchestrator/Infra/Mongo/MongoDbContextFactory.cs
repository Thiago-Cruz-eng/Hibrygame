using MongoDB.Driver;

namespace Orchestrator.Infra.Mongo;

/// <summary>
/// Implementação de <see cref="IMongoDbContextFactory"/>.
///
/// <para>
/// <b>Não use.</b> Ninguém a injeta, e ela não faz o que o nome promete. Três problemas, todos
/// visíveis no corpo do único método:
/// </para>
/// <list type="number">
///   <item><description>
///   o <c>country</c> recebido é ignorado — sempre devolve o mesmo banco;
///   </description></item>
///   <item><description>
///   a connection string e o nome do banco estão escritos no código, então configuração não
///   tem efeito aqui;
///   </description></item>
///   <item><description>
///   o <see cref="IConfiguration"/> injetado nunca é lido — provavelmente era por onde a
///   connection string deveria vir.
///   </description></item>
/// </list>
///
/// <para>
/// O caminho que a aplicação usa de verdade é o registro direto de
/// <see cref="IMongoDbContext"/> em <c>Program.cs</c>, que lê <c>Mongo:ConnectionString</c> e
/// <c>Mongo:Database</c> da configuração. Se você está procurando onde mudar o banco a que a
/// aplicação se conecta, é lá — não aqui.
/// </para>
///
/// <para>
/// Ver <see cref="IMongoDbContextFactory"/> para as duas saídas possíveis desta situação.
/// </para>
/// </summary>
public class MongoDbContextFactory : IMongoDbContextFactory
{
    /// <summary>
    /// Recebido pela DI e nunca lido. Mantido junto do resto para que a intenção original
    /// (buscar a conexão na configuração) continue legível.
    /// </summary>
    private readonly IConfiguration _configuration;

    public MongoDbContextFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>async</c> sem <c>await</c>: não há nada assíncrono aqui, criar um <c>MongoClient</c>
    /// é síncrono. A assinatura devolve <c>Task</c> só para casar com o contrato.
    /// </remarks>
    public Task<IMongoDbContext> CreateAsync(string country)
    {
        // Valores fixos, e não vindos de _configuration — ver a nota da classe.
        var client = new MongoClient("mongodb://localhost:27017");
        return Task.FromResult<IMongoDbContext>(new MongoDbContext(client.GetDatabase("Hibrygame")));
    }
}
