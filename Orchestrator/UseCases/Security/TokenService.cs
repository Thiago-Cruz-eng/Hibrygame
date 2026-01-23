using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Orchestrator.Domain;
using Orchestrator.Infra.Settings;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.UseCases.Security;

public class TokenService : ITokenService
{
    private readonly JwtSettings _settings;
    private readonly ISecureHashingService _hashingService;

    public TokenService(IOptions<JwtSettings> settings, ISecureHashingService hashingService)
    {
        _settings = settings.Value;
        _hashingService = hashingService;
    }

    public AccessTokenResult CreateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.Name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_settings.ExpiresMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public RefreshTokenIssueResult CreateRefreshToken(User user)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var (hash, salt) = _hashingService.HashValue(rawToken);
        var expiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenDays);
        var refreshToken = RefreshToken.Create(user.Id, hash, salt, expiresAt);

        return new RefreshTokenIssueResult(rawToken, refreshToken);
    }
}
