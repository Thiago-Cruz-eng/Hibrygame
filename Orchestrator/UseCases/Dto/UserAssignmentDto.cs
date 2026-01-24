namespace Orchestrator.UseCases.Dto;

public class UserAssignmentDto
{
    public string TeamName { get; set; } = null!;
    public string TeamId { get; set; } = null!;
    public string RoleName { get; set; } = null!;
    public string RoleId { get; set; } = null!;
    public List<HierarchyNodeDto> HierarchyNodes { get; set; } = new();
}

public class HierarchyNodeDto
{
    public string NodeId { get; set; } = null!;
    public string NodeName { get; set; } = null!;
}
