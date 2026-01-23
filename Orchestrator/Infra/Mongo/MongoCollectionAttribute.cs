namespace Orchestrator.Infra.Mongo;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MongoCollectionAttribute : Attribute
{
    public MongoCollectionAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
