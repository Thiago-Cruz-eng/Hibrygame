using System.Linq.Expressions;
using System.Reflection;
using MongoDB.Driver;
using Orchestrator.Domain;
using Orchestrator.Infra.Mongo;
using Orchestrator.Infra.Utils;

namespace Orchestrator.Infra.BaseRepository;

/// <summary>
/// Única implementação de <see cref="IGenericRepository"/>: traduz cada operação do contrato
/// para uma chamada do driver oficial do MongoDB.
///
/// <para>
/// O que documenta o comportamento de cada método é o contrato — leia
/// <see cref="IGenericRepository"/> primeiro. Aqui ficam apenas as notas que só fazem sentido
/// olhando a implementação.
/// </para>
///
/// <para>
/// <b>Por que os métodos são <c>virtual</c>:</b> para permitir que um teste herde desta classe
/// e sobrescreva uma operação. A suíte hoje prefere mockar <see cref="IGenericRepository"/>
/// diretamente com Moq, então na prática ninguém herda — mas a possibilidade está mantida.
/// </para>
///
/// <para>
/// <b>Sem transação:</b> cada método é uma ida independente ao banco. Duas escritas seguidas
/// não formam uma unidade atômica, então uma pode gravar e a outra falhar. Onde isso importa,
/// o caso de uso é que tem de tratar. Ver <c>docs/debito-tecnico.md</c>.
/// </para>
/// </summary>
public class GenericRepository : IGenericRepository
{
    private readonly IMongoDbContext _context;

    public GenericRepository(IMongoDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public IMongoCollection<T> GetCollection<T>() where T : BaseEntity
    {
        var collectionName = ResolveCollectionName<T>();
        return _context.Database.GetCollection<T>(collectionName);
    }

    /// <summary>
    /// Descobre em qual coleção do Mongo a entidade <typeparamref name="T"/> mora.
    ///
    /// <para>
    /// A regra é: usa o nome declarado em <c>[CollectionName("...")]</c> na classe; se a
    /// classe não tiver o atributo, usa o próprio nome do tipo. Por isso adicionar
    /// <c>[CollectionName(nameof(MinhaEntidade))]</c> é convenção do projeto e não exigência
    /// técnica — sem ele funciona, mas o nome da coleção passa a depender do nome da classe
    /// em C#, e renomear a classe renomearia silenciosamente a coleção.
    /// </para>
    /// </summary>
    private static string ResolveCollectionName<T>() where T : BaseEntity =>
        typeof(T).GetCustomAttribute<CollectionNameAttribute>()?.CollectionName ?? typeof(T).Name;

    /// <inheritdoc />
    public virtual async Task<List<T>> GetAll<T>(
        Expression<Func<T, bool>>? filter = null,
        int skip = 0,
        int limit = int.MaxValue,
        SortDefinition<T>? sort = null,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity
    {
        // Filtro ausente significa "traga tudo". O driver não aceita null, então viramos isso
        // numa condição sempre verdadeira.
        filter ??= x => true;

        var data = await GetCollection<T>()
            .FindAsync(
                filter,
                FindOptionsExtensions.MakeFindOptions(limit, skip, sort),
                cancellationToken);

        return await data.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<T?> GetFirstOrDefault<T>(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity
    {
        return await GetCollection<T>()
            .Aggregate()
            .Match(filter)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TDestination?> GetFirstProjectedOrDefault<T, TDestination>(
        Expression<Func<T, TDestination>> projection,
        Expression<Func<T, bool>>? filter = null,
        CancellationToken cancellationToken = default) where T : BaseEntity
    {
        filter ??= x => true;

        // A projeção sai de uma expressão C# (`x => new { x.Id, x.Name }`) e é convertida
        // pelo driver na lista de campos que o Mongo deve devolver.
        var projectionDefinition = Builders<T>.Projection.Expression(projection);

        var cursor = await GetCollection<T>().FindAsync(filter,
            options: new FindOptions<T, TDestination> { Projection = projectionDefinition },
            cancellationToken: cancellationToken);

        return await cursor.FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TDestination>> GetProjected<T, TDestination>(
        Expression<Func<T, TDestination>> projection,
        Expression<Func<T, bool>>? filter = null,
        int skip = 0,
        int limit = int.MaxValue,
        SortDefinition<T>? sort = null,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity
    {
        return await GetCollection<T>()
            .GetProjected(
                projection,
                filter,
                skip,
                limit,
                sort,
                cancellationToken
            );
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TDestination>> GetProjectedPaginated<T, TDestination>(
        Expression<Func<T, TDestination>> projection,
        Expression<Func<T, bool>>? filter = null,
        int page = 1,
        int pageSize = int.MaxValue,
        SortDefinition<T>? sort = null,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity
    {
        // Página 1 não pula nada, página 2 pula um bloco, e assim por diante — daí o `- 1`.
        return await GetProjected(
            projection,
            filter,
            skip: (page - 1) * pageSize,
            limit: pageSize,
            sort,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public virtual async Task<long> CountAsync<T>(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    ) where T : BaseEntity
    {
        return await GetCollection<T>().CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<bool> Save<T>(T obj, CancellationToken cancellationToken = default) where T : BaseEntity
    {
        await GetCollection<T>().InsertOneAsync(obj, cancellationToken: cancellationToken);

        // Chegar aqui já significa que inseriu: o driver sinaliza falha por exceção, nunca
        // por retorno. O `bool` existe para uniformizar a assinatura com os demais métodos.
        return true;
    }

    /// <inheritdoc />
    public virtual async Task<T> SaveAndReturn<T>(T obj, CancellationToken cancellationToken = default) where T : BaseEntity
    {
        await GetCollection<T>().InsertOneAsync(obj, cancellationToken: cancellationToken);
        return obj;
    }

    /// <inheritdoc />
    public virtual async Task SaveMany<T>(List<T> listObj, CancellationToken cancellationToken = default)
        where T : BaseEntity
    {
        await GetCollection<T>().InsertManyAsync(listObj, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<bool> ReplaceOne<T>(Expression<Func<T, bool>> filter, T record,
        CancellationToken cancellationToken = default) where T : BaseEntity
    {
        var result = await GetCollection<T>().ReplaceOneAsync(filter, record, cancellationToken: cancellationToken);

        // ModifiedCount, e não MatchedCount: substituir um documento por outro idêntico casa
        // mas não modifica, e então isto devolve false. Está documentado no contrato porque é
        // uma armadilha real de quem usa o retorno como "o documento existia?".
        return result.ModifiedCount > 0;
    }

    /// <inheritdoc />
    public virtual async Task<T> SaveOrReplaceOne<T>(Expression<Func<T, bool>> filter, T obj, CancellationToken cancellationToken = default) where T : BaseEntity
    {
        var replaced = await ReplaceOne(filter, obj, cancellationToken);
        if (replaced)
            return obj;

        await Save(obj, cancellationToken);
        return obj;
    }

    /// <inheritdoc />
    public virtual async Task<bool> DeleteOne<T>(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default) where T : BaseEntity
    {
        var result = await GetCollection<T>().DeleteOneAsync(filter, cancellationToken);
        return result.DeletedCount > 0;
    }

    /// <inheritdoc />
    public virtual async Task<bool> DeleteMany<T>(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default) where T : BaseEntity
    {
        var result = await GetCollection<T>().DeleteManyAsync(filter, cancellationToken);
        return result.DeletedCount > 0;
    }

    /// <inheritdoc />
    public virtual async Task<bool> HasRecord<T>(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default) where T : BaseEntity
    {
        var count = await CountAsync(filter, cancellationToken);
        return count > 0;
    }

    /// <inheritdoc />
    public virtual async Task<bool> Update<T>(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default,
        params (Expression<Func<T, object>>, object)[] updateDefinitions
    ) where T : BaseEntity
    {
        var definitions = BuildUpdateDefinition(updateDefinitions);

        var updateResult = await GetCollection<T>()
            .UpdateOneAsync(
                filter,
                definitions, cancellationToken: cancellationToken);

        return updateResult.ModifiedCount > 0;
    }

    /// <summary>
    /// Junta os pares (campo, valor) num único comando <c>$set</c> do Mongo.
    ///
    /// <para>
    /// O <c>Aggregate</c> aqui é o do LINQ (acumular sobre uma lista), não o pipeline de
    /// agregação do MongoDB — nomes iguais, coisas diferentes. Ele começa sem definição
    /// nenhuma e vai encadeando um <c>Set</c> por par: o primeiro par cria a definição,
    /// os seguintes acrescentam à que já existe.
    /// </para>
    /// </summary>
    /// <returns>
    /// <c>null</c> quando não veio nenhum par. O driver trata isso como comando de
    /// atualização vazio.
    /// </returns>
    private static UpdateDefinition<T>? BuildUpdateDefinition<T>(
        (Expression<Func<T, object>>, object)[] updateDefinitions) where T : BaseEntity
    {
        var update = Builders<T>.Update;

        return updateDefinitions.Aggregate(
            (UpdateDefinition<T>?)null,
            (current, ud) => current?.Set(ud.Item1, ud.Item2) ?? update.Set(ud.Item1, ud.Item2)
        );
    }
}
