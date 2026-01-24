using MongoDB.Bson.Serialization.Attributes;
using Orchestrator.Infra.Mongo;

namespace Orchestrator.Domain;

[MongoCollection("users")]
public class User
{
    [BsonId]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// User's name.
    /// </summary>
    public string Name { get; protected set; } = null!;

    /// <summary>
    /// User's unique email address.
    /// </summary>
    public string Email { get; protected set; } = null!;

    /// <summary>
    /// User's primary role in the system hierarchy.
    /// </summary>
    public string Role { get; protected set; } = null!;

    /// <summary>
    /// Secure hash of the user's password.
    /// </summary>
    public string PasswordHash { get; protected set; } = null!;

    /// <summary>
    /// Cryptographic salt used in password hashing.
    /// </summary>
    public string Salt { get; protected set; } = null!;

    /// <summary>
    /// Indicates whether the user must change their password on next login.
    /// </summary>
    public bool MustChangePassword { get; protected set; }

    /// <summary>
    /// Associations of this user to companies, roles, and hierarchy contexts.
    /// </summary>
    public List<UserAssignment> Assignments { get; protected set; } = new();

    public CreationInformation CreationInformations { get; protected set; } = null!;
    public ModificationInformation? ModificationInformations { get; protected set; }

    /// <summary>
    /// Protected constructor for ORM deserialization.
    /// </summary>
    protected User() { }

    /// <summary>
    /// Internal constructor enforcing validation and initial audit metadata.
    /// </summary>
    private User(
        string name,
        string email,
        string role,
        string passwordHash,
        string salt,
        List<UserAssignment> assignments,
        string createdBy)
    {
        Name = name;
        Email = email;
        Role = role;
        PasswordHash = passwordHash;
        Salt = salt;
        MustChangePassword = false;
        Assignments = new List<UserAssignment>(assignments);
        CreationInformations = new CreationInformation(createdBy);
    }

    /// <summary>
    /// Factory method to create a new <see cref="User"/> instance.
    /// </summary>
    public static User Create(
        string name,
        string email,
        string role,
        string passwordHash,
        string salt,
        List<UserAssignment> assignments,
        string createdBy)
        => new(name, email, role, passwordHash, salt, assignments, createdBy);

    /// <summary>
    /// Marks the user as required to change password and updates audit data.
    /// </summary>
    public User MarkPasswordChangeRequired(string modifiedBy)
    {
        MustChangePassword = true;
        ModificationInformations = new ModificationInformation(modifiedBy);
        return this;
    }

    /// <summary>
    /// Changes the user's assignments and updates audit data.
    /// </summary>
    public User ChangeAssignments(List<UserAssignment> assignments, string modifiedBy)
    {
        Assignments = new List<UserAssignment>(assignments);
        ModificationInformations = new ModificationInformation(modifiedBy);
        return this;
    }

    /// <summary>
    /// Updates the user's name.
    /// </summary>
    public User ChangeName(string name, string modifiedBy)
    {
        Name = name;
        ModificationInformations = new ModificationInformation(modifiedBy);
        return this;
    }

    /// <summary>
    /// Updates the user's email.
    /// </summary>
    public User ChangeEmail(string email, string modifiedBy)
    {
        Email = email;
        ModificationInformations = new ModificationInformation(modifiedBy);
        return this;
    }

    /// <summary>
    /// Updates the user's primary role.
    /// </summary>
    public User ChangeRole(string role, string modifiedBy)
    {
        Role = role;
        ModificationInformations = new ModificationInformation(modifiedBy);
        return this;
    }

    /// <summary>
    /// Changes the user's password hash and resets the change-required flag.
    /// </summary>
    public User ChangePassword(string passwordHash, string salt, string modifiedBy)
    {
        PasswordHash = passwordHash;
        Salt = salt;
        MustChangePassword = false;
        ModificationInformations = new ModificationInformation(modifiedBy);
        return this;
    }

    public override bool Equals(object? obj)
        => obj is User other && Id == other.Id;

    public override int GetHashCode()
        => HashCode.Combine(Id);
}
