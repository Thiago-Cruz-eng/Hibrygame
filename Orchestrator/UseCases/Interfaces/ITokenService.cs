using Orchestrator.Domain;

namespace Orchestrator.UseCases.Interfaces;

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(User user);
    RefreshTokenIssueResult CreateRefreshToken(User user);
}

public record AccessTokenResult(string Token, DateTime ExpiresAt);

public record RefreshTokenIssueResult(string RawToken, RefreshToken Token);
