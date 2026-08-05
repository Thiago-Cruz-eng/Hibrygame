using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Orchestrator.Domain;
using Orchestrator.Infra.Settings;
using Orchestrator.UseCases.Interfaces;
using Orchestrator.UseCases.Security;
using Xunit;

namespace Orchestrator.Test.Security;

public class TokenServiceTests
{
    private readonly Mock<ISecureHashingService> _hashingServiceMock;
    private readonly JwtSettings _settings;
    private readonly TokenService _sut;

    public TokenServiceTests()
    {
        _hashingServiceMock = new Mock<ISecureHashingService>();

        _settings = new JwtSettings
        {
            Key = "super-secret-test-key-minimum-32-bytes-long!",
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpiresMinutes = 60,
            RefreshTokenDays = 7
        };

        _sut = new TokenService(Options.Create(_settings), _hashingServiceMock.Object);
    }

    private static User BuildUser(string role = "jogador")
        => User.Create("Test User", "test@example.com", role, "hash", "salt", new List<UserAssignment>(), "admin");

    // ---------------------------------------------------------------
    // CreateAccessToken
    // ---------------------------------------------------------------

    [Fact]
    public void CreateAccessToken_WhenCalled_ReturnsNonEmptyToken()
    {
        // Arrange
        var user = BuildUser();

        // Act
        var result = _sut.CreateAccessToken(user);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
    }

    [Fact]
    public void CreateAccessToken_WhenCalled_ExpiresAtIsApproximatelyNowPlusExpiresMinutes()
    {
        // Arrange
        var user = BuildUser();
        var before = DateTime.UtcNow.AddMinutes(_settings.ExpiresMinutes);

        // Act
        var result = _sut.CreateAccessToken(user);

        // Assert
        var diff = result.ExpiresAt - before;
        Assert.InRange(Math.Abs(diff.TotalSeconds), 0, 5);
    }

    [Fact]
    public void CreateAccessToken_WhenCalled_PayloadContainsSubClaimEqualToUserId()
    {
        // Arrange
        var user = BuildUser();

        // Act
        var result = _sut.CreateAccessToken(user);

        // Assert — decode raw payload to avoid version-dependent claim type mapping
        var payload = DecodePayload(result.Token);
        Assert.True(payload.TryGetProperty("sub", out var sub));
        Assert.Equal(user.Id.ToString(), sub.GetString());
    }

    [Fact]
    public void CreateAccessToken_WhenCalled_PayloadContainsEmailClaim()
    {
        // Arrange
        var user = BuildUser();

        // Act
        var result = _sut.CreateAccessToken(user);

        // Assert
        var payload = DecodePayload(result.Token);
        Assert.True(payload.TryGetProperty("email", out var email));
        Assert.Equal(user.Email, email.GetString());
    }

    [Fact]
    public void CreateAccessToken_WhenCalled_PayloadContainsNameClaim()
    {
        // Arrange
        var user = BuildUser();

        // Act
        var result = _sut.CreateAccessToken(user);

        // Assert
        var payload = DecodePayload(result.Token);
        Assert.True(payload.TryGetProperty("name", out var name));
        Assert.Equal(user.Name, name.GetString());
    }

    [Fact]
    public void CreateAccessToken_WhenCalled_PayloadContainsRoleClaim()
    {
        // Arrange
        var user = BuildUser(role: "adm");

        // Act
        var result = _sut.CreateAccessToken(user);

        // Assert — role is serialized with the full ClaimTypes.Role URI as the key
        var payload = DecodePayload(result.Token);
        var roleFound = payload.TryGetProperty(ClaimTypes.Role, out var roleValue)
                        || payload.TryGetProperty("role", out roleValue);
        Assert.True(roleFound, $"Role claim not found. Payload keys: {string.Join(", ", payload.EnumerateObject().Select(p => p.Name))}");
        Assert.Equal("adm", roleValue.GetString());
    }

    [Fact]
    public void CreateAccessToken_WhenCalled_PayloadContainsJtiAsValidGuid()
    {
        // Arrange
        var user = BuildUser();

        // Act
        var result = _sut.CreateAccessToken(user);

        // Assert
        var payload = DecodePayload(result.Token);
        Assert.True(payload.TryGetProperty("jti", out var jti));
        Assert.True(Guid.TryParse(jti.GetString(), out _));
    }

    [Fact]
    public void CreateAccessToken_WhenCalled_TokenIsSignedWithHmacSha256AndCorrectKey()
    {
        // Arrange
        var user = BuildUser();

        // Act
        var result = _sut.CreateAccessToken(user);

        // Assert — validate signature only; skip audience/issuer
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false
        };

        var principal = tokenHandler.ValidateToken(result.Token, validationParams, out _);
        Assert.NotNull(principal);
    }

    [Fact]
    public void CreateAccessToken_WhenCalled_TokenSignatureFailsWithWrongKey()
    {
        // Arrange
        var user = BuildUser();

        // Act
        var result = _sut.CreateAccessToken(user);

        // Assert — wrong key must cause validation failure
        var tokenHandler = new JwtSecurityTokenHandler();
        var wrongKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("wrong-key-that-is-also-32-bytes-long!!"));
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = wrongKey,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false
        };

        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(
            () => tokenHandler.ValidateToken(result.Token, validationParams, out _));
    }

    [Fact]
    public void CreateAccessToken_CalledTwice_ProducesDifferentJtiClaims()
    {
        // Arrange
        var user = BuildUser();

        // Act
        var result1 = _sut.CreateAccessToken(user);
        var result2 = _sut.CreateAccessToken(user);

        // Assert
        var payload1 = DecodePayload(result1.Token);
        var payload2 = DecodePayload(result2.Token);
        payload1.TryGetProperty("jti", out var jti1);
        payload2.TryGetProperty("jti", out var jti2);
        Assert.NotEqual(jti1.GetString(), jti2.GetString());
    }

    // ---------------------------------------------------------------
    // CreateRefreshToken
    // ---------------------------------------------------------------

    [Fact]
    public void CreateRefreshToken_WhenCalled_ReturnsNonEmptyRawToken()
    {
        // Arrange
        var user = BuildUser();
        _hashingServiceMock
            .Setup(h => h.HashValue(It.IsAny<string>()))
            .Returns(("hashed-value", "salt-value"));

        // Act
        var result = _sut.CreateRefreshToken(user);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.RawToken);
    }

    [Fact]
    public void CreateRefreshToken_WhenCalled_RawTokenIsBase64()
    {
        // Arrange
        var user = BuildUser();
        _hashingServiceMock
            .Setup(h => h.HashValue(It.IsAny<string>()))
            .Returns(("hashed-value", "salt-value"));

        // Act
        var result = _sut.CreateRefreshToken(user);

        // Assert
        var exception = Record.Exception(() => Convert.FromBase64String(result.RawToken));
        Assert.Null(exception);
    }

    [Fact]
    public void CreateRefreshToken_WhenCalled_RefreshTokenEntityHasCorrectUserId()
    {
        // Arrange
        var user = BuildUser();
        _hashingServiceMock
            .Setup(h => h.HashValue(It.IsAny<string>()))
            .Returns(("hashed-value", "salt-value"));

        // Act
        var result = _sut.CreateRefreshToken(user);

        // Assert
        Assert.Equal(user.Id, result.Token.UserId);
    }

    [Fact]
    public void CreateRefreshToken_WhenCalled_RefreshTokenEntityHasHashFromHashingService()
    {
        // Arrange
        var user = BuildUser();
        _hashingServiceMock
            .Setup(h => h.HashValue(It.IsAny<string>()))
            .Returns(("expected-hash", "expected-salt"));

        // Act
        var result = _sut.CreateRefreshToken(user);

        // Assert
        Assert.Equal("expected-hash", result.Token.TokenHash);
        Assert.Equal("expected-salt", result.Token.Salt);
    }

    [Fact]
    public void CreateRefreshToken_WhenCalled_RefreshTokenExpiresAtIsApproximatelyNowPlusRefreshTokenDays()
    {
        // Arrange
        var user = BuildUser();
        _hashingServiceMock
            .Setup(h => h.HashValue(It.IsAny<string>()))
            .Returns(("hash", "salt"));
        var before = DateTime.UtcNow.AddDays(_settings.RefreshTokenDays);

        // Act
        var result = _sut.CreateRefreshToken(user);

        // Assert
        var diff = result.Token.ExpiresAt - before;
        Assert.InRange(Math.Abs(diff.TotalSeconds), 0, 5);
    }

    [Fact]
    public void CreateRefreshToken_WhenCalled_CallsHashingServiceWithRawToken()
    {
        // Arrange
        var user = BuildUser();
        string? capturedRaw = null;
        _hashingServiceMock
            .Setup(h => h.HashValue(It.IsAny<string>()))
            .Callback<string>(v => capturedRaw = v)
            .Returns(("hash", "salt"));

        // Act
        var result = _sut.CreateRefreshToken(user);

        // Assert
        _hashingServiceMock.Verify(h => h.HashValue(It.IsAny<string>()), Times.Once);
        Assert.NotNull(capturedRaw);
        Assert.Equal(result.RawToken, capturedRaw);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Decodes the JWT payload from Base64Url to a JsonElement, bypassing
    /// all claim-type mapping performed by JwtSecurityTokenHandler.
    /// </summary>
    private static JsonElement DecodePayload(string token)
    {
        var parts = token.Split('.');
        var base64 = parts[1].Replace('-', '+').Replace('_', '/');
        while (base64.Length % 4 != 0) base64 += "=";
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        return JsonDocument.Parse(json).RootElement;
    }
}
