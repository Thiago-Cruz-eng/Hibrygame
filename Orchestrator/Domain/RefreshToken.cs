using MongoDB.Bson.Serialization.Attributes;
using Orchestrator.Infra.Mongo;
using Orchestrator.Infra.Utils;

namespace Orchestrator.Domain;

[CollectionName(nameof(RefreshToken))]
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; init; }

    public string TokenHash { get; private set; } = null!;
    public string Salt { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public string? ReasonRevoked { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => RevokedAt is null && !IsExpired;

    protected RefreshToken() { }

    private RefreshToken(Guid userId, string tokenHash, string salt, DateTime expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        Salt = salt;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
    }

    public static RefreshToken Create(Guid userId, string tokenHash, string salt, DateTime expiresAt)
        => new(userId, tokenHash, salt, expiresAt);

    public void Revoke(string reason, Guid? replacedByTokenId = null)
    {
        if (RevokedAt is not null)
            return;

        RevokedAt = DateTime.UtcNow;
        ReasonRevoked = reason;
        ReplacedByTokenId = replacedByTokenId;
    }
}
