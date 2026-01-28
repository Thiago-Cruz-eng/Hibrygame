using MongoDB.Driver;

namespace Orchestrator.Infra.Utils;

public static class FindOptionsExtensions
{
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