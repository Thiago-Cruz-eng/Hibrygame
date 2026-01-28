namespace Orchestrator.Infra.Utils;

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public class CollectionNameAttribute : Attribute
{
    public string CollectionName { get; }

    public CollectionNameAttribute(string name)
    {
        CollectionName = name;
    }
}