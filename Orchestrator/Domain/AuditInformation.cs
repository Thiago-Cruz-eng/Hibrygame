namespace Orchestrator.Domain;

public sealed class CreationInformation
{
    public CreationInformation(string createdBy)
    {
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
    }

    public string CreatedBy { get; }
    public DateTime CreatedAt { get; }
}

public sealed class ModificationInformation
{
    public ModificationInformation(string modifiedBy)
    {
        ModifiedBy = modifiedBy;
        ModifiedAt = DateTime.UtcNow;
    }

    public string ModifiedBy { get; }
    public DateTime ModifiedAt { get; }
}
