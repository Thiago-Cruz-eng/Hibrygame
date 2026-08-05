using MongoDB.Driver;

namespace Orchestrator.Infra.Utils;

/// <summary>
/// Monta o objeto de opções que o driver do MongoDB aceita em <c>FindAsync</c> — paginação e
/// ordenação, e nos casos projetados também a lista de campos a trazer.
///
/// <para>
/// Existe para não repetir a construção do <c>FindOptions</c> em cada método do repositório, e
/// para concentrar num único lugar a regra do "sem opção nenhuma" descrita abaixo.
/// </para>
/// </summary>
public static class FindOptionsExtensions
{
    /// <summary>
    /// Opções para uma busca que devolve a entidade inteira.
    /// </summary>
    /// <param name="limit">Teto de documentos.</param>
    /// <param name="skip">Quantos pular.</param>
    /// <param name="sort">Ordenação, ou <c>null</c> para deixar a critério do Mongo.</param>
    /// <returns>
    /// <c>null</c> quando <paramref name="limit"/> e <paramref name="skip"/> são ambos zero.
    ///
    /// <para>
    /// Devolver <c>null</c> é intencional e o driver o interpreta como "sem opções", o que é
    /// diferente de passar <c>Limit = 0</c> — este último pediria zero documentos. É a razão de
    /// o caso (0, 0) ser tratado à parte em vez de cair no construtor normal.
    /// </para>
    /// </returns>
    public static FindOptions<T>? MakeFindOptions<T>(
        int limit,
        int skip,
        SortDefinition<T>? sort = null
    )
    {
        if (limit == 0 && skip == 0)
            return null;

        return new FindOptions<T>
        {
            Limit = limit,
            Skip = skip,
            Sort = sort,
        };
    }

    /// <summary>
    /// Igual à sobrecarga acima, mas para busca <b>projetada</b>: além de paginar e ordenar,
    /// carrega a definição de quais campos o Mongo deve devolver.
    /// </summary>
    /// <param name="projection">
    /// Campos a trazer. <c>null</c> traz o documento inteiro.
    /// </param>
    /// <returns><c>null</c> pelo mesmo motivo da outra sobrecarga.</returns>
    public static FindOptions<T, TProjection>? MakeFindOptions<T, TProjection>(
        int limit,
        int skip,
        SortDefinition<T>? sort = null,
        ProjectionDefinition<T, TProjection>? projection = null
    )
    {
        if (limit == 0 && skip == 0)
            return null;

        return new FindOptions<T, TProjection>
        {
            Limit = limit,
            Skip = skip,
            Sort = sort,
            Projection = projection
        };
    }
}
