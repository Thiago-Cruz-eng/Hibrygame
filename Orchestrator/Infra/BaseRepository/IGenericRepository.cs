using System.Linq.Expressions;
using MongoDB.Driver;
using Orchestrator.Domain;

namespace Orchestrator.Infra.BaseRepository;

/// <summary>
/// Contrato do CRUD genérico sobre MongoDB. É a única porta pela qual o projeto fala com o
/// banco: nenhum caso de uso instancia <c>IMongoCollection</c> por conta própria.
///
/// <para>
/// <b>Como esta camada se encaixa</b> — são três níveis, e vale saber em qual você deveria mexer:
/// </para>
/// <list type="number">
///   <item>
///     <description>
///     <b>Este contrato</b> (<see cref="IGenericRepository"/>) — operações que funcionam para
///     QUALQUER entidade, por isso todo método é genérico em <c>T</c>. Mexa aqui só se
///     precisar de uma operação nova de banco que sirva para todas as entidades.
///     </description>
///   </item>
///   <item>
///     <description>
///     <b><c>BaseRepositoryNoSql&lt;T&gt;</c></b> — recorta deste contrato o punhado de
///     operações que os casos de uso realmente usam (buscar por id, por filtro, salvar,
///     atualizar, remover), com assinaturas mais simples.
///     </description>
///   </item>
///   <item>
///     <description>
///     <b><c>{Entidade}RepositoryNoSql</c></b> — um por entidade. Nascem vazios de propósito.
///     É aqui que entra query específica de uma entidade, e em nenhum dos dois níveis acima.
///     </description>
///   </item>
/// </list>
///
/// <para>
/// <b>Convenções que valem para todos os métodos daqui:</b>
/// </para>
/// <list type="bullet">
///   <item><description>
///   <c>T</c> é sempre uma entidade (<see cref="BaseEntity"/>). A coleção de destino é
///   descoberta a partir do atributo <c>[CollectionName]</c> da classe — você nunca escreve
///   o nome da coleção à mão.
///   </description></item>
///   <item><description>
///   Nenhum método lança exceção por "não encontrei". Ausência é <c>null</c>, <c>false</c> ou
///   lista vazia. Quem decide se isso é erro é o caso de uso.
///   </description></item>
///   <item><description>
///   <c>filter</c> é uma expressão LINQ (<c>x =&gt; x.Email == email</c>) que o driver do
///   Mongo traduz para query. Escreva-a sobre as propriedades da entidade, não sobre nomes
///   de campo do banco.
///   </description></item>
/// </list>
/// </summary>
public interface IGenericRepository
{
    /// <summary>
    /// Coleção do Mongo correspondente a <typeparamref name="T"/>, para o caso raro de
    /// precisar de uma operação do driver que este contrato não expõe.
    ///
    /// <para>
    /// Preferir os outros métodos. Usar este devolve o driver cru para dentro do chamador, o
    /// que espalha conhecimento de MongoDB por camadas que não deveriam ter nenhum.
    /// </para>
    /// </summary>
    IMongoCollection<T> GetCollection<T>() where T : BaseEntity;

    /// <summary>
    /// Todos os documentos que casam com <paramref name="filter"/>.
    /// </summary>
    /// <param name="filter">
    /// Condição de busca. <c>null</c> traz a coleção inteira — o que em coleção grande é uma
    /// má ideia; nesse caso passe <paramref name="limit"/>.
    /// </param>
    /// <param name="skip">Quantos documentos pular. Usado para paginação.</param>
    /// <param name="limit">Teto de documentos devolvidos.</param>
    /// <param name="sort">Ordenação. <c>null</c> deixa a ordem a critério do Mongo.</param>
    /// <returns>Lista, possivelmente vazia. Nunca <c>null</c>.</returns>
    Task<List<T>> GetAll<T>(
        Expression<Func<T, bool>>? filter = null,
        int skip = 0,
        int limit = int.MaxValue,
        SortDefinition<T>? sort = null,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity;

    /// <summary>
    /// Primeiro documento que casa com <paramref name="filter"/>, ou <c>null</c> se nenhum
    /// casar. É o método para "buscar por id" e "buscar por e-mail".
    /// </summary>
    Task<T?> GetFirstOrDefault<T>(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity;

    /// <summary>
    /// Como <see cref="GetFirstOrDefault{T}"/>, mas devolve só as propriedades descritas em
    /// <paramref name="projection"/> em vez da entidade inteira.
    ///
    /// <para>
    /// Serve para não trazer do banco campo que você não vai usar — por exemplo ler apenas o
    /// nome de um usuário sem carregar o hash de senha junto.
    /// </para>
    /// </summary>
    /// <param name="projection">
    /// O que trazer, como expressão: <c>x =&gt; new { x.Id, x.Name }</c>.
    /// </param>
    Task<TDestination?> GetFirstProjectedOrDefault<T, TDestination>(
        Expression<Func<T, TDestination>> projection,
        Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellationToken = default) where T : BaseEntity;

    /// <summary>
    /// Versão em lista de <see cref="GetFirstProjectedOrDefault{T,TDestination}"/>: vários
    /// documentos, cada um reduzido a <paramref name="projection"/>.
    /// </summary>
    Task<IEnumerable<TDestination>> GetProjected<T, TDestination>(
        Expression<Func<T, TDestination>> projection,
        Expression<Func<T, bool>>? filter = null,
        int skip = 0,
        int limit = int.MaxValue,
        SortDefinition<T>? sort = null,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity;

    /// <summary>
    /// O mesmo que <see cref="GetProjected{T,TDestination}"/>, mas com paginação em página e
    /// tamanho de página em vez de <c>skip</c>/<c>limit</c>.
    ///
    /// <para>
    /// <paramref name="page"/> começa em <b>1</b>, não em 0: a primeira página não pula nada.
    /// </para>
    /// </summary>
    Task<IEnumerable<TDestination>> GetProjectedPaginated<T, TDestination>(
        Expression<Func<T, TDestination>> projection,
        Expression<Func<T, bool>>? filter = null,
        int page = 1,
        int pageSize = int.MaxValue,
        SortDefinition<T>? sort = null,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity;

    /// <summary>
    /// Quantos documentos casam com <paramref name="filter"/>. Contar no banco em vez de
    /// trazer tudo e medir a lista em memória.
    /// </summary>
    Task<long> CountAsync<T>(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity;

    /// <summary>
    /// Insere <paramref name="obj"/>. Sempre insere — nunca atualiza um documento existente;
    /// para isso existe <see cref="SaveOrReplaceOne{T}"/>.
    /// </summary>
    /// <returns>
    /// Sempre <c>true</c>: falha de inserção vem como exceção do driver, não como
    /// <c>false</c>.
    /// </returns>
    Task<bool> Save<T>(T obj, CancellationToken cancellationToken = default) where T : BaseEntity;

    /// <summary>
    /// Insere <paramref name="obj"/> e devolve a própria instância recebida, para permitir
    /// encadeamento. Equivalente a <see cref="Save{T}"/> em efeito no banco.
    /// </summary>
    Task<T> SaveAndReturn<T>(T obj, CancellationToken cancellationToken = default) where T : BaseEntity;

    /// <summary>
    /// Insere vários documentos numa única ida ao banco.
    ///
    /// <para>
    /// Não é atômico: o projeto não usa transação de MongoDB (ver <c>docs/debito-tecnico.md</c>).
    /// Se a inserção falhar no meio, os documentos anteriores ficam gravados.
    /// </para>
    /// </summary>
    Task SaveMany<T>(List<T> listObj, CancellationToken cancellationToken = default) where T : BaseEntity;

    /// <summary>
    /// Substitui o documento que casa com <paramref name="filter"/> por
    /// <paramref name="record"/> — o documento inteiro, não campo a campo. Campo que exista
    /// no banco e não na instância nova <b>desaparece</b>.
    /// </summary>
    /// <returns>
    /// <c>false</c> quando nada casou com o filtro <b>e também</b> quando o documento
    /// encontrado já era idêntico ao enviado — o Mongo não conta isso como modificação. Não
    /// use este retorno como prova de que o documento não existe.
    /// </returns>
    Task<bool> ReplaceOne<T>(Expression<Func<T, bool>> filter, T record,
        CancellationToken cancellationToken = default) where T : BaseEntity;

    /// <summary>
    /// Substitui se já existir, insere se não existir (upsert feito à mão).
    /// </summary>
    Task<T> SaveOrReplaceOne<T>(Expression<Func<T, bool>> filter, T obj, CancellationToken cancellationToken = default) where T : BaseEntity;

    /// <summary>
    /// Remove <b>um</b> documento — o primeiro que casar com <paramref name="filter"/>.
    /// Remoção é física: não há exclusão lógica neste projeto.
    /// </summary>
    /// <returns><c>true</c> se algo foi removido; <c>false</c> se nada casou.</returns>
    Task<bool> DeleteOne<T>(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        where T : BaseEntity;

    /// <summary>
    /// Remove <b>todos</b> os documentos que casarem com <paramref name="filter"/>.
    ///
    /// <para>
    /// Cuidado: filtro que aceita tudo (<c>x =&gt; true</c>) esvazia a coleção, e não há
    /// como desfazer.
    /// </para>
    /// </summary>
    /// <returns><c>true</c> se ao menos um documento foi removido.</returns>
    Task<bool> DeleteMany<T>(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        where T : BaseEntity;

    /// <summary>
    /// Existe ao menos um documento que casa com <paramref name="filter"/>? Mais barato que
    /// buscar o documento só para testar se ele é <c>null</c>.
    /// </summary>
    Task<bool> HasRecord<T>(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        where T : BaseEntity;

    /// <summary>
    /// Atualização parcial: mexe apenas nos campos listados em
    /// <paramref name="updateDefinitions"/> e deixa o resto do documento intacto. É a
    /// diferença em relação a <see cref="ReplaceOne{T}"/>, que troca o documento todo.
    ///
    /// <para>
    /// Cada item é um par (qual campo, qual valor):
    /// <c>Update&lt;User&gt;(x =&gt; x.Id == id, ct, (u =&gt; u.Name, "Ana"))</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Atenção à ordem dos parâmetros:</b> o <c>CancellationToken</c> vem ANTES do
    /// <c>params</c>, porque em C# um <c>params</c> tem de ser o último parâmetro. Por isso
    /// o token não é opcional na prática — você precisa passá-lo (ou <c>default</c>) para
    /// então informar os campos.
    /// </para>
    /// </summary>
    /// <returns>
    /// <c>false</c> quando nada casou com o filtro <b>ou</b> quando os valores enviados eram
    /// iguais aos que já estavam gravados.
    /// </returns>
    Task<bool> Update<T>(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default,
        params (Expression<Func<T, object>>, object)[] updateDefinitions
    ) where T : BaseEntity;
}
