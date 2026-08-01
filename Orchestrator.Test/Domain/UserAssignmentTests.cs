using Orchestrator.Domain;
using Xunit;

namespace Orchestrator.Test.Domain;

public class UserAssignmentTests
{
    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static UserAssignment.HierarchyNode BuildNode(string id = "node-1", string name = "Node One")
        => UserAssignment.HierarchyNode.Create(id, name);

    private static UserAssignment BuildAssignment(
        string teamName = "Team Alpha",
        string teamId = "team-1",
        string roleName = "jogador",
        string roleId = "role-1",
        List<UserAssignment.HierarchyNode>? nodes = null,
        string createdBy = "admin")
        => UserAssignment.Create(teamName, teamId, roleName, roleId, nodes ?? new List<UserAssignment.HierarchyNode>(), createdBy);

    // ---------------------------------------------------------------
    // Create factory method
    // ---------------------------------------------------------------

    [Fact]
    public void Create_WhenCalled_SetsTeamName()
    {
        // Act
        var assignment = BuildAssignment(teamName: "My Team");

        // Assert
        Assert.Equal("My Team", assignment.TeamName);
    }

    [Fact]
    public void Create_WhenCalled_SetsTeamId()
    {
        // Act
        var assignment = BuildAssignment(teamId: "team-99");

        // Assert
        Assert.Equal("team-99", assignment.TeamId);
    }

    [Fact]
    public void Create_WhenCalled_SetsRoleName()
    {
        // Act
        var assignment = BuildAssignment(roleName: "adm");

        // Assert
        Assert.Equal("adm", assignment.RoleName);
    }

    [Fact]
    public void Create_WhenCalled_SetsRoleId()
    {
        // Act
        var assignment = BuildAssignment(roleId: "role-99");

        // Assert
        Assert.Equal("role-99", assignment.RoleId);
    }

    [Fact]
    public void Create_WhenCalled_CopiesHierarchyNodesIndependently()
    {
        // Arrange
        var original = new List<UserAssignment.HierarchyNode> { BuildNode("node-1") };

        // Act
        var assignment = BuildAssignment(nodes: original);
        original.Clear();

        // Assert — clearing original should not affect assignment's list
        Assert.Single(assignment.HierarchyNodes);
    }

    // ---------------------------------------------------------------
    // ChangeTeam
    // ---------------------------------------------------------------

    [Fact]
    public void ChangeTeam_WhenCalled_UpdatesTeamName()
    {
        // Arrange
        var assignment = BuildAssignment(teamName: "Old Team");

        // Act
        assignment.ChangeTeam("New Team", "team-2", "admin");

        // Assert
        Assert.Equal("New Team", assignment.TeamName);
    }

    [Fact]
    public void ChangeTeam_WhenCalled_UpdatesTeamId()
    {
        // Arrange
        var assignment = BuildAssignment(teamId: "team-old");

        // Act
        assignment.ChangeTeam("New Team", "team-new", "admin");

        // Assert
        Assert.Equal("team-new", assignment.TeamId);
    }

    [Fact]
    public void ChangeTeam_WhenCalled_ReturnsThis()
    {
        // Arrange
        var assignment = BuildAssignment();

        // Act
        var result = assignment.ChangeTeam("New Team", "team-2", "admin");

        // Assert
        Assert.Same(assignment, result);
    }

    // ---------------------------------------------------------------
    // ChangeRole
    // ---------------------------------------------------------------

    [Fact]
    public void ChangeRole_WhenCalled_UpdatesRoleName()
    {
        // Arrange
        var assignment = BuildAssignment(roleName: "jogador");

        // Act
        assignment.ChangeRole("adm", "role-adm", "admin");

        // Assert
        Assert.Equal("adm", assignment.RoleName);
    }

    [Fact]
    public void ChangeRole_WhenCalled_UpdatesRoleId()
    {
        // Arrange
        var assignment = BuildAssignment(roleId: "role-old");

        // Act
        assignment.ChangeRole("adm", "role-new", "admin");

        // Assert
        Assert.Equal("role-new", assignment.RoleId);
    }

    [Fact]
    public void ChangeRole_WhenCalled_ReturnsThis()
    {
        // Arrange
        var assignment = BuildAssignment();

        // Act
        var result = assignment.ChangeRole("adm", "role-adm", "admin");

        // Assert
        Assert.Same(assignment, result);
    }

    // ---------------------------------------------------------------
    // ChangeHierarchy
    // ---------------------------------------------------------------

    [Fact]
    public void ChangeHierarchy_WhenCalled_ReplacesNodes()
    {
        // Arrange
        var assignment = BuildAssignment(nodes: new List<UserAssignment.HierarchyNode> { BuildNode("old-node") });
        var newNodes = new List<UserAssignment.HierarchyNode> { BuildNode("new-node-1"), BuildNode("new-node-2") };

        // Act
        assignment.ChangeHierarchy(newNodes, "admin");

        // Assert
        Assert.Equal(2, assignment.HierarchyNodes.Count);
        Assert.Contains(assignment.HierarchyNodes, n => n.NodeId == "new-node-1");
        Assert.Contains(assignment.HierarchyNodes, n => n.NodeId == "new-node-2");
    }

    [Fact]
    public void ChangeHierarchy_WhenCalled_CopiesListIndependently()
    {
        // Arrange
        var assignment = BuildAssignment();
        var newNodes = new List<UserAssignment.HierarchyNode> { BuildNode("n1") };

        // Act
        assignment.ChangeHierarchy(newNodes, "admin");
        newNodes.Clear();

        // Assert
        Assert.Single(assignment.HierarchyNodes);
    }

    [Fact]
    public void ChangeHierarchy_WhenCalled_ReturnsThis()
    {
        // Arrange
        var assignment = BuildAssignment();

        // Act
        var result = assignment.ChangeHierarchy(new List<UserAssignment.HierarchyNode>(), "admin");

        // Assert
        Assert.Same(assignment, result);
    }

    // ---------------------------------------------------------------
    // AddHierarchyNode
    // ---------------------------------------------------------------

    [Fact]
    public void AddHierarchyNode_WhenNodeAbsent_AddsNode()
    {
        // Arrange
        var assignment = BuildAssignment();
        var node = BuildNode("node-new");

        // Act
        assignment.AddHierarchyNode(node, "admin");

        // Assert
        Assert.Single(assignment.HierarchyNodes);
        Assert.Contains(assignment.HierarchyNodes, n => n.NodeId == "node-new");
    }

    [Fact]
    public void AddHierarchyNode_WhenNodeAlreadyPresent_DoesNotAddDuplicate()
    {
        // Arrange
        var node = BuildNode("node-1");
        var assignment = BuildAssignment(nodes: new List<UserAssignment.HierarchyNode> { node });

        // Act — add the exact same record (record equality)
        assignment.AddHierarchyNode(node, "admin");

        // Assert
        Assert.Single(assignment.HierarchyNodes);
    }

    [Fact]
    public void AddHierarchyNode_WhenCalled_ReturnsThis()
    {
        // Arrange
        var assignment = BuildAssignment();

        // Act
        var result = assignment.AddHierarchyNode(BuildNode(), "admin");

        // Assert
        Assert.Same(assignment, result);
    }

    // ---------------------------------------------------------------
    // RemoveHierarchyNode
    // ---------------------------------------------------------------

    [Fact]
    public void RemoveHierarchyNode_WhenNodeExists_RemovesIt()
    {
        // Arrange
        var node = BuildNode("node-to-remove");
        var assignment = BuildAssignment(nodes: new List<UserAssignment.HierarchyNode> { node, BuildNode("keep-me") });

        // Act
        assignment.RemoveHierarchyNode("node-to-remove", "admin");

        // Assert
        Assert.Single(assignment.HierarchyNodes);
        Assert.DoesNotContain(assignment.HierarchyNodes, n => n.NodeId == "node-to-remove");
    }

    [Fact]
    public void RemoveHierarchyNode_WhenNodeNotFound_NoOp()
    {
        // Arrange
        var assignment = BuildAssignment(nodes: new List<UserAssignment.HierarchyNode> { BuildNode("existing") });

        // Act — no exception
        assignment.RemoveHierarchyNode("non-existent", "admin");

        // Assert
        Assert.Single(assignment.HierarchyNodes);
    }

    [Fact]
    public void RemoveHierarchyNode_WhenCalled_ReturnsThis()
    {
        // Arrange
        var assignment = BuildAssignment(nodes: new List<UserAssignment.HierarchyNode> { BuildNode("node-1") });

        // Act
        var result = assignment.RemoveHierarchyNode("node-1", "admin");

        // Assert
        Assert.Same(assignment, result);
    }

    // ---------------------------------------------------------------
    // ToString
    // ---------------------------------------------------------------

    [Fact]
    public void ToString_WhenCalled_ContainsTeamName()
    {
        // Arrange
        var assignment = BuildAssignment(teamName: "Alpha Team");

        // Assert
        Assert.Contains("Alpha Team", assignment.ToString());
    }

    [Fact]
    public void ToString_WhenCalled_ContainsTeamId()
    {
        // Arrange
        var assignment = BuildAssignment(teamId: "team-42");

        // Assert
        Assert.Contains("team-42", assignment.ToString());
    }

    [Fact]
    public void ToString_WhenCalled_ContainsRoleName()
    {
        // Arrange
        var assignment = BuildAssignment(roleName: "adm");

        // Assert
        Assert.Contains("adm", assignment.ToString());
    }

    [Fact]
    public void ToString_WhenCalled_ContainsRoleId()
    {
        // Arrange
        var assignment = BuildAssignment(roleId: "role-123");

        // Assert
        Assert.Contains("role-123", assignment.ToString());
    }

    // ---------------------------------------------------------------
    // HierarchyNode
    // ---------------------------------------------------------------

    [Fact]
    public void HierarchyNodeCreate_WhenCalled_SetsNodeId()
    {
        // Act
        var node = UserAssignment.HierarchyNode.Create("id-1", "Node Name");

        // Assert
        Assert.Equal("id-1", node.NodeId);
    }

    [Fact]
    public void HierarchyNodeCreate_WhenCalled_SetsNodeName()
    {
        // Act
        var node = UserAssignment.HierarchyNode.Create("id-1", "Node Name");

        // Assert
        Assert.Equal("Node Name", node.NodeName);
    }

    [Fact]
    public void HierarchyNode_RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var node1 = UserAssignment.HierarchyNode.Create("id-1", "Same Name");
        var node2 = UserAssignment.HierarchyNode.Create("id-1", "Same Name");

        // Assert — records with same values should be equal
        Assert.Equal(node1, node2);
    }

    [Fact]
    public void HierarchyNode_RecordEquality_DifferentNodeId_AreNotEqual()
    {
        // Arrange
        var node1 = UserAssignment.HierarchyNode.Create("id-1", "Name");
        var node2 = UserAssignment.HierarchyNode.Create("id-2", "Name");

        // Assert
        Assert.NotEqual(node1, node2);
    }
}
