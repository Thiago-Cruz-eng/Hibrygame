using Orchestrator.Domain;
using Xunit;

namespace Orchestrator.Test.Domain;

public class RefreshTokenTests
{
    private static readonly Guid SampleUserId = Guid.NewGuid();

    // ---------------------------------------------------------------
    // Create factory method
    // ---------------------------------------------------------------

    [Fact]
    public void Create_WhenCalled_SetsUserId()
    {
        // Act
        var token = RefreshToken.Create(SampleUserId, "hash", "salt", DateTime.UtcNow.AddDays(7));

        // Assert
        Assert.Equal(SampleUserId, token.UserId);
    }

    [Fact]
    public void Create_WhenCalled_SetsTokenHash()
    {
        // Act
        var token = RefreshToken.Create(SampleUserId, "my-hash", "my-salt", DateTime.UtcNow.AddDays(7));

        // Assert
        Assert.Equal("my-hash", token.TokenHash);
    }

    [Fact]
    public void Create_WhenCalled_SetsSalt()
    {
        // Act
        var token = RefreshToken.Create(SampleUserId, "my-hash", "my-salt", DateTime.UtcNow.AddDays(7));

        // Assert
        Assert.Equal("my-salt", token.Salt);
    }

    [Fact]
    public void Create_WhenCalled_SetsExpiresAt()
    {
        // Arrange
        var expires = DateTime.UtcNow.AddDays(7);

        // Act
        var token = RefreshToken.Create(SampleUserId, "hash", "salt", expires);

        // Assert
        Assert.Equal(expires, token.ExpiresAt);
    }

    [Fact]
    public void Create_WhenCalled_CreatedAtIsApproximatelyNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var token = RefreshToken.Create(SampleUserId, "hash", "salt", DateTime.UtcNow.AddDays(7));
        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(token.CreatedAt, before, after);
    }

    [Fact]
    public void Create_WhenCalled_RevokedAtIsNull()
    {
        // Act
        var token = RefreshToken.Create(SampleUserId, "hash", "salt", DateTime.UtcNow.AddDays(7));

        // Assert
        Assert.Null(token.RevokedAt);
    }

    [Fact]
    public void Create_WhenCalled_IdIsNonEmptyGuid()
    {
        // Act
        var token = RefreshToken.Create(SampleUserId, "hash", "salt", DateTime.UtcNow.AddDays(7));

        // Assert
        Assert.NotEqual(Guid.Empty, token.Id);
    }

    // ---------------------------------------------------------------
    // IsExpired
    // ---------------------------------------------------------------

    [Fact]
    public void IsExpired_WhenExpiresAtIsInFuture_ReturnsFalse()
    {
        // Arrange
        var token = RefreshToken.Create(SampleUserId, "hash", "salt", DateTime.UtcNow.AddDays(1));

        // Assert
        Assert.False(token.IsExpired);
    }

    [Fact]
    public void IsExpired_WhenExpiresAtIsInPast_ReturnsTrue()
    {
        // Arrange
        var token = RefreshToken.Create(SampleUserId, "hash", "salt", DateTime.UtcNow.AddSeconds(-1));

        // Assert
        Assert.True(token.IsExpired);
    }

    // ---------------------------------------------------------------
    // IsActive
    // ---------------------------------------------------------------

    [Fact]
    public void IsActive_WhenNotRevokedAndNotExpired_ReturnsTrue()
    {
        // Arrange
        var token = RefreshToken.Create(SampleUserId, "hash", "salt", DateTime.UtcNow.AddDays(7));

        // Assert
        Assert.True(token.IsActive);
    }

    [Fact]
    public void IsActive_WhenExpired_ReturnsFalse()
    {
        // Arrange
        var token = RefreshToken.Create(SampleUserId, "hash", "salt", DateTime.UtcNow.AddSeconds(-1));

        // Assert
        Assert.False(token.IsActive);
    }

    [Fact]
    public void IsActive_WhenRevoked_ReturnsFalse()
    {
        // Arrange
        var token = RefreshToken.Create(SampleUserId, "hash", "salt", DateTime.UtcNow.AddDays(7));

        // Act
        token.Revoke("logout");

        // Assert
        Assert.False(token.IsActive);
    }

    // ---------------------------------------------------------------
    // Revoke — without replacement
    // ---------------------------------------------------------------

    [Fact]
    public void Revoke_WithReason_SetsRevokedAt()
    {
        // Arrange
        var token = RefreshToken.Create(SampleUserId, "hash", "salt", DateTime.UtcNow.AddDays(7));
        var before = DateTime.UtcNow;

        // Act
        token.Revoke("logout");

        // Assert
        Assert.NotNull(token.RevokedAt);
        Assert.InRange(token.RevokedAt!.Value, before, DateTime.UtcNow);
    }

    [Fact]
    public void Revoke_WithReason_SetsReasonRevoked()
    {
        // Arrange
        var token = RefreshToken.Create(SampleUserId, "hash", "salt", DateTime.UtcNow.AddDays(7));

        // Act
        token.Revoke("user-requested-logout");

        // Assert
        Assert.Equal("user-requested-logout", token.ReasonRevoked);
    }

    [Fact]
    public void Revoke_WithoutReplacedById_ReplacedByTokenIdIsNull()
    {
        // Arrange
        var token = RefreshToken.Create(SampleUserId, "hash", "salt", DateTime.UtcNow.AddDays(7));

        // Act
        token.Revoke("logout");

        // Assert
        Assert.Null(token.ReplacedByTokenId);
    }

    // ---------------------------------------------------------------
    // Revoke — with replacement token
    // ---------------------------------------------------------------

    [Fact]
    public void Revoke_WithReplacementId_SetsReplacedByTokenId()
    {
        // Arrange
        var token = RefreshToken.Create(SampleUserId, "hash", "salt", DateTime.UtcNow.AddDays(7));
        var replacementId = Guid.NewGuid();

        // Act
        token.Revoke("rotated", replacementId);

        // Assert
        Assert.Equal(replacementId, token.ReplacedByTokenId);
    }

    // ---------------------------------------------------------------
    // Revoke — idempotency
    // ---------------------------------------------------------------

    [Fact]
    public void Revoke_CalledTwice_RevokedAtNotChangedOnSecondCall()
    {
        // Arrange
        var token = RefreshToken.Create(SampleUserId, "hash", "salt", DateTime.UtcNow.AddDays(7));
        token.Revoke("first-revocation");
        var firstRevokedAt = token.RevokedAt;

        // Act — small delay is intentional in real time, but we just call immediately
        token.Revoke("second-revocation");

        // Assert — allow 1 ms tolerance; RevokedAt must equal firstRevokedAt
        Assert.NotNull(token.RevokedAt);
        var diff = Math.Abs((token.RevokedAt!.Value - firstRevokedAt!.Value).TotalMilliseconds);
        Assert.InRange(diff, 0, 1);
    }

    [Fact]
    public void Revoke_CalledTwice_ReasonNotChangedOnSecondCall()
    {
        // Arrange
        var token = RefreshToken.Create(SampleUserId, "hash", "salt", DateTime.UtcNow.AddDays(7));
        token.Revoke("first-reason");

        // Act
        token.Revoke("second-reason");

        // Assert
        Assert.Equal("first-reason", token.ReasonRevoked);
    }

    [Fact]
    public void Revoke_CalledTwice_ReplacementNotChangedOnSecondCall()
    {
        // Arrange
        var token = RefreshToken.Create(SampleUserId, "hash", "salt", DateTime.UtcNow.AddDays(7));
        var firstReplacement = Guid.NewGuid();
        token.Revoke("rotated", firstReplacement);

        // Act
        token.Revoke("rotated-again", Guid.NewGuid());

        // Assert
        Assert.Equal(firstReplacement, token.ReplacedByTokenId);
    }
}
