using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Orchestrator.Infra.Utils;

public static class MongoCollectionExtensions
{
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
        filter ??= x => true;

        var data = await collection.FindAsync(
            filter,
            options: PrepareFindOptions(projection, skip, limit, sort),
            cancellationToken
        );

        return data.ToEnumerable(cancellationToken);
    }

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
        filter ??= FilterDefinition<TDocument>.Empty;

        var data = await collection.FindAsync(
            filter,
            options: PrepareFindOptions(projection, skip, limit, sort),
            cancellationToken
        );

        return data.ToEnumerable(cancellationToken);
    }

    private static FindOptions<TDocument, TDestination>? PrepareFindOptions<TDocument, TDestination>(
        Expression<Func<TDocument, TDestination>> projection,
        int skip = 0,
        int limit = 1,
        SortDefinition<TDocument>? sort = null
    )
    {
        var projectionDefBuilder = new ProjectionDefinitionBuilder<TDocument>();
        var project = projectionDefBuilder.Expression(projection);

        return FindOptionsExtensions.MakeFindOptions(limit, skip, sort, project);
    }
}