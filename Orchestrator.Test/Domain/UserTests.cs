using Orchestrator.Domain;
using Xunit;

namespace Orchestrator.Test.Domain;

public class UserTests
{
    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static List<UserAssignment> EmptyAssignments() => new();

    private static UserAssignment BuildAssignment(string teamId = "team-1")
        => UserAssignment.Create("Team Alpha", teamId, "jogador", "role-1", new List<UserAssignment.HierarchyNode>(), "admin");

    private static User BuildUser(
        string name = "Alice",
        string email = "alice@example.com",
        string role = "jogador",
        string passwordHash = "hash",
        string salt = "salt",
        List<UserAssignment>? assignments = null,
        string createdBy = "admin")
        => User.Create(name, email, role, passwordHash, salt, assignments ?? EmptyAssignments(), createdBy);

    // ---------------------------------------------------------------
    // Create factory method
    // ---------------------------------------------------------------

    [Fact]
    public void Create_WhenCalled_SetsName()
    {
        // Act
        var user = BuildUser(name: "Bob");

        // Assert
        Assert.Equal("Bob", user.Name);
    }

    [Fact]
    public void Create_WhenCalled_SetsEmail()
    {
        // Act
        var user = BuildUser(email: "bob@example.com");

        // Assert
        Assert.Equal("bob@example.com", user.Email);
    }

    [Fact]
    public void Create_WhenCalled_SetsRole()
    {
        // Act
        var user = BuildUser(role: "adm");

        // Assert
        Assert.Equal("adm", user.Role);
    }

    [Fact]
    public void Create_WhenCalled_SetsPasswordHash()
    {
        // Act
        var user = BuildUser(passwordHash: "my-hash");

        // Assert
        Assert.Equal("my-hash", user.PasswordHash);
    }

    [Fact]
    public void Create_WhenCalled_SetsSalt()
    {
        // Act
        var user = BuildUser(salt: "my-salt");

        // Assert
        Assert.Equal("my-salt", user.Salt);
    }

    [Fact]
    public void Create_WhenCalled_MustChangePasswordIsFalse()
    {
        // Act
        var user = BuildUser();

        // Assert
        Assert.False(user.MustChangePassword);
    }

    [Fact]
    public void Create_WhenCalled_IdIsNonEmptyGuid()
    {
        // Act
        var user = BuildUser();

        // Assert
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Fact]
    public void Create_WhenCalled_CreationInformationsHasCorrectCreatedBy()
    {
        // Act
        var user = BuildUser(createdBy: "system");

        // Assert
        Assert.NotNull(user.CreationInformations);
        Assert.Equal("system", user.CreationInformations.CreatedBy);
    }

    [Fact]
    public void Create_WhenCalled_CreationInformationsCreatedAtIsApproximatelyNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var user = BuildUser();
        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(user.CreationInformations.CreatedAt, before, after);
    }

    [Fact]
    public void Create_WhenCalled_ModificationInformationsIsNull()
    {
        // Act
        var user = BuildUser();

        // Assert
        Assert.Null(user.ModificationInformations);
    }

    [Fact]
    public void Create_WhenCalled_AssignmentsCopiedIndependently()
    {
        // Arrange
        var original = new List<UserAssignment> { BuildAssignment() };

        // Act
        var user = BuildUser(assignments: original);
        original.Clear();

        // Assert — clearing original should not affect user's list
        Assert.Single(user.Assignments);
    }

    [Fact]
    public void Create_TwoCalls_ProduceDifferentIds()
    {
        // Act
        var user1 = BuildUser();
        var user2 = BuildUser();

        // Assert
        Assert.NotEqual(user1.Id, user2.Id);
    }

    // ---------------------------------------------------------------
    // MarkPasswordChangeRequired
    // ---------------------------------------------------------------

    [Fact]
    public void MarkPasswordChangeRequired_WhenCalled_SetsMustChangePasswordTrue()
    {
        // Arrange
        var user = BuildUser();

        // Act
        user.MarkPasswordChangeRequired("admin");

        // Assert
        Assert.True(user.MustChangePassword);
    }

    [Fact]
    public void MarkPasswordChangeRequired_WhenCalled_SetsModificationInformations()
    {
        // Arrange
        var user = BuildUser();

        // Act
        user.MarkPasswordChangeRequired("admin");

        // Assert
        Assert.NotNull(user.ModificationInformations);
        Assert.Equal("admin", user.ModificationInformations!.ModifiedBy);
    }

    [Fact]
    public void MarkPasswordChangeRequired_WhenCalled_ReturnsThis()
    {
        // Arrange
        var user = BuildUser();

        // Act
        var result = user.MarkPasswordChangeRequired("admin");

        // Assert
        Assert.Same(user, result);
    }

    // ---------------------------------------------------------------
    // ChangeName
    // ---------------------------------------------------------------

    [Fact]
    public void ChangeName_WhenCalled_UpdatesName()
    {
        // Arrange
        var user = BuildUser(name: "Alice");

        // Act
        user.ChangeName("Charlie", "admin");

        // Assert
        Assert.Equal("Charlie", user.Name);
    }

    [Fact]
    public void ChangeName_WhenCalled_SetsModificationInformations()
    {
        // Arrange
        var user = BuildUser();

        // Act
        user.ChangeName("Charlie", "editor");

        // Assert
        Assert.NotNull(user.ModificationInformations);
        Assert.Equal("editor", user.ModificationInformations!.ModifiedBy);
    }

    [Fact]
    public void ChangeName_WhenCalled_ReturnsThis()
    {
        // Arrange
        var user = BuildUser();

        // Act
        var result = user.ChangeName("NewName", "admin");

        // Assert
        Assert.Same(user, result);
    }

    // ---------------------------------------------------------------
    // ChangeEmail
    // ---------------------------------------------------------------

    [Fact]
    public void ChangeEmail_WhenCalled_UpdatesEmail()
    {
        // Arrange
        var user = BuildUser(email: "old@example.com");

        // Act
        user.ChangeEmail("new@example.com", "admin");

        // Assert
        Assert.Equal("new@example.com", user.Email);
    }

    [Fact]
    public void ChangeEmail_WhenCalled_ReturnsThis()
    {
        // Arrange
        var user = BuildUser();

        // Act
        var result = user.ChangeEmail("new@example.com", "admin");

        // Assert
        Assert.Same(user, result);
    }

    // ---------------------------------------------------------------
    // ChangeRole
    // ---------------------------------------------------------------

    [Fact]
    public void ChangeRole_WhenCalled_UpdatesRole()
    {
        // Arrange
        var user = BuildUser(role: "jogador");

        // Act
        user.ChangeRole("adm", "admin");

        // Assert
        Assert.Equal("adm", user.Role);
    }

    [Fact]
    public void ChangeRole_WhenCalled_ReturnsThis()
    {
        // Arrange
        var user = BuildUser();

        // Act
        var result = user.ChangeRole("adm", "admin");

        // Assert
        Assert.Same(user, result);
    }

    // ---------------------------------------------------------------
    // ChangeAssignments
    // ---------------------------------------------------------------

    [Fact]
    public void ChangeAssignments_WhenCalled_UpdatesAssignments()
    {
        // Arrange
        var user = BuildUser();
        var newAssignments = new List<UserAssignment> { BuildAssignment("team-99") };

        // Act
        user.ChangeAssignments(newAssignments, "admin");

        // Assert
        Assert.Single(user.Assignments);
        Assert.Equal("team-99", user.Assignments[0].TeamId);
    }

    [Fact]
    public void ChangeAssignments_WhenCalled_CopiesListIndependently()
    {
        // Arrange
        var user = BuildUser();
        var newAssignments = new List<UserAssignment> { BuildAssignment("team-99") };

        // Act
        user.ChangeAssignments(newAssignments, "admin");
        newAssignments.Clear();

        // Assert — clearing original should not affect user's list
        Assert.Single(user.Assignments);
    }

    [Fact]
    public void ChangeAssignments_WhenCalled_ReturnsThis()
    {
        // Arrange
        var user = BuildUser();

        // Act
        var result = user.ChangeAssignments(new List<UserAssignment>(), "admin");

        // Assert
        Assert.Same(user, result);
    }

    // ---------------------------------------------------------------
    // ChangePassword
    // ---------------------------------------------------------------

    [Fact]
    public void ChangePassword_WhenCalled_UpdatesPasswordHash()
    {
        // Arrange
        var user = BuildUser(passwordHash: "old-hash");

        // Act
        user.ChangePassword("new-hash", "new-salt", "admin");

        // Assert
        Assert.Equal("new-hash", user.PasswordHash);
    }

    [Fact]
    public void ChangePassword_WhenCalled_UpdatesSalt()
    {
        // Arrange
        var user = BuildUser(salt: "old-salt");

        // Act
        user.ChangePassword("new-hash", "new-salt", "admin");

        // Assert
        Assert.Equal("new-salt", user.Salt);
    }

    [Fact]
    public void ChangePassword_WhenMustChangePasswordWasTrue_ResetsFlagToFalse()
    {
        // Arrange
        var user = BuildUser();
        user.MarkPasswordChangeRequired("admin");
        Assert.True(user.MustChangePassword);

        // Act
        user.ChangePassword("new-hash", "new-salt", "admin");

        // Assert
        Assert.False(user.MustChangePassword);
    }

    [Fact]
    public void ChangePassword_WhenCalled_SetsModificationInformations()
    {
        // Arrange
        var user = BuildUser();

        // Act
        user.ChangePassword("new-hash", "new-salt", "security-svc");

        // Assert
        Assert.NotNull(user.ModificationInformations);
        Assert.Equal("security-svc", user.ModificationInformations!.ModifiedBy);
    }

    [Fact]
    public void ChangePassword_WhenCalled_ReturnsThis()
    {
        // Arrange
        var user = BuildUser();

        // Act
        var result = user.ChangePassword("new-hash", "new-salt", "admin");

        // Assert
        Assert.Same(user, result);
    }

    // ---------------------------------------------------------------
    // Equals / GetHashCode
    // ---------------------------------------------------------------

    [Fact]
    public void Equals_SameId_ReturnsTrue()
    {
        // Arrange
        var user1 = BuildUser();
        // Create a second user sharing the same Id via the base property setter
        var user2 = BuildUser();
        user2.Id = user1.Id;

        // Assert
        Assert.Equal(user1, user2);
    }

    [Fact]
    public void Equals_DifferentIds_ReturnsFalse()
    {
        // Arrange
        var user1 = BuildUser();
        var user2 = BuildUser();

        // Assert — two separately created users have different Ids
        Assert.NotEqual(user1, user2);
    }

    [Fact]
    public void GetHashCode_SameId_ProducesSameHashCode()
    {
        // Arrange
        var user1 = BuildUser();
        var user2 = BuildUser();
        user2.Id = user1.Id;

        // Assert
        Assert.Equal(user1.GetHashCode(), user2.GetHashCode());
    }

    [Fact]
    public void Equals_NullObject_ReturnsFalse()
    {
        // Arrange
        var user = BuildUser();

        // Assert
        Assert.False(user.Equals(null));
    }

    [Fact]
    public void Equals_NonUserObject_ReturnsFalse()
    {
        // Arrange
        var user = BuildUser();

        // O argumento e declarado como `object` de proposito. O que esta sob teste e o guard
        // `obj is User` de User.Equals, entao passar algo que NAO e User e o ponto do teste.
        // Passando a string direto, o tipo estatico da chamada vira (User, string) e o CodeQL
        // aponta `cs/equality-on-incomparable-types` — corretamente, pela regra dele: comparar
        // tipos incomparaveis normalmente E bug. Declarar como object diz ao leitor e ao
        // analisador que a incomparabilidade e deliberada, sem suprimir o alerta.
        object naoEhUsuario = "not-a-user";

        // Assert
        Assert.False(user.Equals(naoEhUsuario));
    }
}
