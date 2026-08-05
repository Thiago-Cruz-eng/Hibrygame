using System.Linq.Expressions;
using MongoDB.Driver;

namespace Orchestrator.Infra.Utils;

/// <summary>
/// Métodos de extensão de <c>IMongoCollection&lt;T&gt;</c> para busca <b>projetada</b> — trazer
/// só algumas propriedades de cada documento, em vez do documento inteiro.
///
/// <para>
/// Projetar vale a pena quando o documento tem campo grande ou sensível que a tela não precisa:
/// listar nomes de usuários sem arrastar hash de senha e salt junto, por exemplo. Menos bytes na
/// rede e menos dado sensível circulando por camadas que não têm o que fazer com ele.
/// </para>
///
/// <para>
/// Quem chama isto na prática é <c>GenericRepository.GetProjected</c>. Um caso de uso não deveria
/// chamar diretamente — ele fala com o repositório, não com o driver.
/// </para>
/// </summary>
public static class MongoCollectionExtensions
{
    /// <summary>
    /// Busca projetada com filtro escrito como <b>expressão LINQ</b>
    /// (<c>x =&gt; x.Email == email</c>). É a sobrecarga natural para código C#.
    /// </summary>
    /// <param name="projection">
    /// O que trazer de cada documento: <c>x =&gt; new { x.Id, x.Name }</c>.
    /// </param>
    /// <param name="filter">Condição de busca; <c>null</c> considera todos os documentos.</param>
    /// <param name="limit">
    /// Teto de documentos. O <b>padrão é 1</b>, não "sem limite" — vindo de
    /// <c>GenericRepository</c> o valor é sempre explícito, mas se você chamar direto, passe o
    /// limite que quer ou receberá um único documento.
    /// </param>
    public static async Task<IEnumerable<TDestination>> GetProjected<TDocument, TDestination>(
        this IMongoCollection<TDocument> collection,
        Expression<Func<TDocument, TDestination>> projection,
        Expression<Func<TDocument, bool>>? filter = null,
        int skip = 0,
        int limit = 1,
        SortDefinition<TDocument>? sort = null,
        CancellationToken cancellationToken = default
    )
    {
        // O driver não aceita filtro nulo: "tudo" se escreve como condição sempre verdadeira.
        filter ??= x => true;

        var data = await collection.FindAsync(
            filter,
            options: PrepareFindOptions(projection, skip, limit, sort),
            cancellationToken
        );

        return data.ToEnumerable(cancellationToken);
    }

    /// <summary>
    /// Mesma busca, com o filtro já montado como <see cref="FilterDefinition{TDocument}"/> do
    /// driver.
    ///
    /// <para>
    /// Serve para as consultas que a expressão LINQ não sabe descrever — operadores próprios do
    /// Mongo como busca em array ou geoespacial. Preferir a sobrecarga com expressão sempre que
    /// ela der conta: é verificada pelo compilador, enquanto esta só falha em tempo de execução.
    /// </para>
    /// </summary>
    public static async Task<IEnumerable<TDestination>> GetProjected<TDocument, TDestination>(
        this IMongoCollection<TDocument> collection,
        Expression<Func<TDocument, TDestination>> projection,
        FilterDefinition<TDocument>? filter = null,
        int skip = 0,
        int limit = 1,
        SortDefinition<TDocument>? sort = null,
        CancellationToken cancellationToken = default
    )
    {
        // Aqui o equivalente a "tudo" é o filtro vazio do driver.
        filter ??= FilterDefinition<TDocument>.Empty;

        var data = await collection.FindAsync(
            filter,
            options: PrepareFindOptions(projection, skip, limit, sort),
            cancellationToken
        );

        return data.ToEnumerable(cancellationToken);
    }

    /// <summary>
    /// Converte a projeção escrita em C# no formato que o driver entende e junta com paginação e
    /// ordenação. É o único ponto de diferença entre as duas sobrecargas acima, extraído para
    /// não ficar duplicado.
    /// </summary>
    private static FindOptions<TDocument, TDestination>? PrepareFindOptions<TDocument, TDestination>(
        Expression<Func<TDocument, TDestination>> projection,
        int skip = 0,
        int limit = 1,
        SortDefinition<TDocument>? sort = null
    )
    {
        var project = new ProjectionDefinitionBuilder<TDocument>().Expression(projection);

        return FindOptionsExtensions.MakeFindOptions(limit, skip, sort, project);
    }
}
