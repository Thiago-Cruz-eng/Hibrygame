using MongoDB.Driver;

namespace Orchestrator.Infra.Mongo;

/// <summary>
/// Acesso ao banco escolhido, atrás de uma interface.
///
/// <para>
/// Existe por uma razão só: <c>IMongoDatabase</c> é um tipo do driver, e depender dele
/// diretamente amarraria o resto do código ao MongoDB e tornaria o banco impossível de trocar
/// por um dublê em teste. Com esta interface no meio, <c>GenericRepository</c> recebe uma
/// abstração e a suíte injeta um mock.
/// </para>
///
/// <para>
/// É registrado como <b>singleton</b> no <c>Program.cs</c>, e deve ser: o cliente do MongoDB
/// mantém um pool de conexões internamente e é feito para viver o processo inteiro. Criar um
/// por request esgotaria conexões.
/// </para>
/// </summary>
public interface IMongoDbContext
{
    /// <summary>O banco em que todas as coleções deste projeto vivem.</summary>
    IMongoDatabase Database { get; }
}
