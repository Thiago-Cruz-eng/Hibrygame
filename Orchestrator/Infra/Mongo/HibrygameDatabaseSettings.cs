namespace Orchestrator.Infra.Mongo;

public class HibrygameDatabaseSettings
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
    public string HibrygameCollectionName { get; set; } = null!;
}