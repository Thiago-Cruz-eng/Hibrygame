using System.Linq.Expressions;
using Orchestrator.Domain;

namespace Orchestrator.Infra.Interfaces;

/// <summary>
/// Contrato que os casos de uso realmente consomem: as cinco operações de que eles precisam,
/// já amarradas a uma entidade <typeparamref name="T"/>.
///
/// <para>
/// É deliberadamente mais pobre que <c>IGenericRepository</c>. Aquele expõe projeção,
/// paginação, contagem e a coleção crua do Mongo; este expõe o mínimo. A razão é a regra de
/// camadas do projeto: caso de uso não deve saber que o banco é MongoDB, e quanto menos
/// superfície de driver estiver ao alcance dele, menos chance de vazar.
/// </para>
///
/// <para>
/// <b>Não implemente esta interface diretamente.</b> Herde de <c>BaseRepositoryNoSql&lt;T&gt;</c>,
/// que já a implementa inteira delegando ao CRUD genérico.
/// </para>
/// </summary>
/// <typeparam name="T">A entidade que este repositório persiste.</typeparam>
public interface IGenericRepositoryNoSql<T> where T : BaseEntity
{
    /// <summary>
    /// Todas as entidades que casam com <paramref name="filter"/>. Lista vazia quando nada
    /// casa — nunca <c>null</c>, nunca exceção.
    /// </summary>
    Task<IEnumerable<T>> FindByFilter(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// A entidade de <paramref name="id"/>, ou <c>null</c> se não existir.
    /// </summary>
    Task<T?> GetById(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Insere <paramref name="entity"/>. Sempre insere: para atualizar, use
    /// <see cref="Update"/>.
    /// </summary>
    Task Save(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Substitui a entidade de <paramref name="id"/> por <paramref name="entity"/> — inteira,
    /// não campo a campo.
    /// </summary>
    /// <param name="id">
    /// O id como texto. Vem assim porque request e rota trafegam texto; a conversão para
    /// <see cref="Guid"/> acontece na implementação.
    /// </param>
    /// <returns>
    /// <c>false</c> em três situações que valem distinguir: o <paramref name="id"/> não é um
    /// Guid válido, nenhum documento tem esse id, ou o documento encontrado já era idêntico ao
    /// enviado. Nenhuma delas lança exceção.
    /// </returns>
    Task<bool> Update(string id, T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a entidade de <paramref name="id"/>. Remoção física, sem desfazer.
    /// </summary>
    /// <param name="entity">
    /// Não é usado: a remoção acontece por <paramref name="id"/>. O parâmetro é herdado da
    /// assinatura original e está mantido para não quebrar os chamadores.
    /// </param>
    /// <returns><c>false</c> se o id for inválido ou se nada existir com esse id.</returns>
    Task<bool> Delete(string id, T entity, CancellationToken cancellationToken = default);
}
