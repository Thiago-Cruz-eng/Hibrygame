namespace Orchestrator.Infra.Mongo;

/// <summary>
/// Fábrica que criaria um <see cref="IMongoDbContext"/> por país.
///
/// <para>
/// <b>ATENÇÃO — nada consome esta interface.</b> Ela é registrada em <c>Program.cs</c> e nunca
/// injetada em lugar nenhum. Quem realmente entrega o contexto ao repositório é o registro
/// direto de <see cref="IMongoDbContext"/> como singleton, alguns registros abaixo dela no
/// mesmo arquivo.
/// </para>
///
/// <para>
/// Está mantida, e não removida, porque o parâmetro <c>country</c> revela uma intenção que o
/// código não terminou de cumprir: escolher banco por região. Apagar a interface apagaria esse
/// sinal junto. Duas saídas possíveis, ambas exigindo decisão humana:
/// </para>
/// <list type="bullet">
///   <item><description>
///   se a separação por país <b>vai</b> existir, implementar de verdade (hoje a implementação
///   ignora o parâmetro) e passar os repositórios a usá-la;
///   </description></item>
///   <item><description>
///   se <b>não</b> vai, remover esta interface, sua implementação e o registro no
///   <c>Program.cs</c> — o mesmo tratamento que MediatR, Polly e <c>ServiceFactory</c>
///   receberam no refactor de 2026-08-01, por igualmente não terem chamador.
///   </description></item>
/// </list>
///
/// <para>
/// <b>Não construa nada novo sobre esta interface</b> antes de essa decisão ser tomada.
/// </para>
/// </summary>
public interface IMongoDbContextFactory
{
    /// <summary>
    /// Criaria o contexto do banco correspondente a <paramref name="country"/>.
    /// </summary>
    /// <param name="country">
    /// País cujo banco se quer. <b>Ignorado</b> pela implementação atual — ver
    /// <see cref="MongoDbContextFactory.CreateAsync"/>.
    /// </param>
    Task<IMongoDbContext> CreateAsync(string country);
}
