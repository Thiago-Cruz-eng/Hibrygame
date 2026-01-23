namespace Orchestrator.Domain;

public class UserAssignment
{
    /// <summary>
    /// Name of the Team context.
    /// </summary>
    public string TeamName { get; protected set; } = null!;

    /// <summary>
    /// Identifier of the Team.
    /// </summary>
    public string TeamId { get; protected set; } = null!;

    /// <summary>
    /// Display name of the role assigned to the user.
    /// </summary>
    public string RoleName { get; protected set; } = null!;

    /// <summary>
    /// Identifier of the role.
    /// </summary>
    public string RoleId { get; protected set; } = null!;

    /// <summary>
    /// Collection of hierarchy nodes the user can access.
    /// </summary>
    public List<HierarchyNode> HierarchyNodes { get; protected set; } = new();

    /// <summary>
    /// Default constructor for ORM/deserialization.
    /// </summary>
    protected UserAssignment() { }

    /// <summary>
    /// Internal constructor enforcing validation and audit metadata.
    /// </summary>
    private UserAssignment(
        string teamName,
        string teamId,
        string roleName,
        string roleId,
        List<HierarchyNode> hierarchyNodes,
        string createdBy)
    {
        TeamName = teamName;
        TeamId = teamId;
        RoleName = roleName;
        RoleId = roleId;
        HierarchyNodes = new List<HierarchyNode>(hierarchyNodes);
    }

    /// <summary>
    /// Factory method to instantiate a new <see cref="UserAssignment"/>.
    /// </summary>
    public static UserAssignment Create(
        string teamName,
        string teamId,
        string roleName,
        string roleId,
        List<HierarchyNode> hierarchyNodes,
        string createdBy)
        => new(teamName, teamId, roleName, roleId, hierarchyNodes, createdBy);

    /// <summary>
    /// Updates team context and audit metadata.
    /// </summary>
    public UserAssignment ChangeTeam(
        string teamName,
        string teamId,
        string modifiedBy)
    {
        TeamName = teamName;
        TeamId = teamId;
        return this;
    }

    /// <summary>
    /// Updates assigned role and audit metadata.
    /// </summary>
    public UserAssignment ChangeRole(
        string roleName,
        string roleId,
        string modifiedBy)
    {
        RoleName = roleName;
        RoleId = roleId;
        return this;
    }

    /// <summary>
    /// Replaces hierarchy nodes and updates audit metadata.
    /// </summary>
    public UserAssignment ChangeHierarchy(
        List<HierarchyNode> hierarchyNodes,
        string modifiedBy)
    {
        HierarchyNodes = new List<HierarchyNode>(hierarchyNodes);
        return this;
    }

    /// <summary>
    /// Adds a hierarchy node if not already present and updates audit metadata.
    /// </summary>
    public UserAssignment AddHierarchyNode(
        HierarchyNode node,
        string modifiedBy)
    {
        if (!HierarchyNodes.Contains(node))
            HierarchyNodes.Add(node);
        return this;
    }

    /// <summary>
    /// Removes a hierarchy node by Id if present and updates audit metadata.
    /// </summary>
    public UserAssignment RemoveHierarchyNode(
        string nodeId,
        string modifiedBy)
    {
        var node = HierarchyNodes.FirstOrDefault(n => n.NodeId == nodeId);
        if (node != null)
        {
            HierarchyNodes.Remove(node);
        }
        return this;
    }

    public override string ToString() =>
        $"{TeamName} ({TeamId}) - " +
        $"{RoleName} ({RoleId}) - " +
        $"Nodes: [{string.Join(", ", HierarchyNodes)}]";

    /// <summary>
    /// Represents an organizational hierarchy node with selling permissions.
    /// </summary>
    public record HierarchyNode
    {
        /// <summary>
        /// Identifier of the hierarchy node.
        /// </summary>
        public string NodeId { get; init; } = null!;

        /// <summary>
        /// Display name of the hierarchy node.
        /// </summary>
        public string NodeName { get; init; } = null!;

        private HierarchyNode(string nodeId, string nodeName)
        {
            NodeId = nodeId;
            NodeName = nodeName;
        }

        /// <summary>
        /// Factory method to create a new <see cref="HierarchyNode"/>.
        /// </summary>
        public static HierarchyNode Create(
            string nodeId,
            string nodeName)
            => new(nodeId, nodeName);
    }
}
