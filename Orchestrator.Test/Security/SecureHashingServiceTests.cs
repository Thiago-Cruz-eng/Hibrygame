using Orchestrator.UseCases.Security;
using Xunit;

namespace Orchestrator.Test.Security;

public class SecureHashingServiceTests
{
    private readonly SecureHashingService _sut = new();

    // ---------------------------------------------------------------
    // HashValue
    // ---------------------------------------------------------------

    [Fact]
    public void HashValue_WhenCalled_ReturnsNonEmptyHash()
    {
        // Arrange
        const string value = "my-secret-password";

        // Act
        var (hash, _) = _sut.HashValue(value);

        // Assert
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void HashValue_WhenCalled_ReturnsNonEmptySalt()
    {
        // Arrange
        const string value = "my-secret-password";

        // Act
        var (_, salt) = _sut.HashValue(value);

        // Assert
        Assert.NotEmpty(salt);
    }

    [Fact]
    public void HashValue_WhenCalled_HashAndSaltAreValidBase64()
    {
        // Arrange
        const string value = "my-secret-password";

        // Act
        var (hash, salt) = _sut.HashValue(value);

        // Assert
        Assert.Null(Record.Exception(() => Convert.FromBase64String(hash)));
        Assert.Null(Record.Exception(() => Convert.FromBase64String(salt)));
    }

    [Fact]
    public void HashValue_SameValueHashedTwice_ProducesDifferentHashes()
    {
        // Arrange
        const string value = "same-password";

        // Act
        var (hash1, _) = _sut.HashValue(value);
        var (hash2, _) = _sut.HashValue(value);

        // Assert — salt is random so hashes must differ
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void HashValue_SameValueHashedTwice_ProducesDifferentSalts()
    {
        // Arrange
        const string value = "same-password";

        // Act
        var (_, salt1) = _sut.HashValue(value);
        var (_, salt2) = _sut.HashValue(value);

        // Assert
        Assert.NotEqual(salt1, salt2);
    }

    // ---------------------------------------------------------------
    // Verify — correct value
    // ---------------------------------------------------------------

    [Fact]
    public void Verify_CorrectValue_ReturnsTrue()
    {
        // Arrange
        const string value = "correct-password";
        var (hash, salt) = _sut.HashValue(value);

        // Act
        var result = _sut.Verify(value, hash, salt);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Verify_WrongValue_ReturnsFalse()
    {
        // Arrange
        const string value = "correct-password";
        var (hash, salt) = _sut.HashValue(value);

        // Act
        var result = _sut.Verify("wrong-password", hash, salt);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_EmptyStringAgainstHashOfEmptyString_ReturnsTrue()
    {
        // Arrange
        var (hash, salt) = _sut.HashValue(string.Empty);

        // Act
        var result = _sut.Verify(string.Empty, hash, salt);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Verify_TamperedHash_ReturnsFalse()
    {
        // Arrange
        const string value = "password123";
        var (_, salt) = _sut.HashValue(value);
        var (differentHash, _) = _sut.HashValue("different-password");

        // Act
        var result = _sut.Verify(value, differentHash, salt);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_TamperedSalt_ReturnsFalse()
    {
        // Arrange
        const string value = "password123";
        var (hash, _) = _sut.HashValue(value);
        var (_, differentSalt) = _sut.HashValue("different-password");

        // Act
        var result = _sut.Verify(value, hash, differentSalt);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("a very long password that exceeds typical limits and should still work fine 1234567890!@#$%^&*()")]
    [InlineData("unicode-passwörd")]
    public void Verify_VariousValues_RoundTripsCorrectly(string value)
    {
        // Arrange
        var (hash, salt) = _sut.HashValue(value);

        // Act
        var result = _sut.Verify(value, hash, salt);

        // Assert
        Assert.True(result);
    }
}
