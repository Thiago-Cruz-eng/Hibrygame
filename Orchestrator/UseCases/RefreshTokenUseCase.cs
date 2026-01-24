using Microsoft.Extensions.Logging;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.UseCases;

public class RefreshTokenUseCase
{
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly IRefreshTokenRepositoryNoSql _refreshTokenRepository;
    private readonly ISecureHashingService _hashingService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RefreshTokenUseCase> _logger;

    public RefreshTokenUseCase(
        IUserRepositoryNoSql userRepository,
        IRefreshTokenRepositoryNoSql refreshTokenRepository,
        ISecureHashingService hashingService,
        ITokenService tokenService,
        ILogger<RefreshTokenUseCase> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _hashingService = hashingService;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<RefreshTokenResponse> RefreshAsync(RefreshTokenRequest req)
    {
        try
        {
            if (!Guid.TryParse(req.UserId, out var userId))
                return new RefreshTokenResponse { Message = "Invalid user", Success = false };

            var users = await _userRepository.FindByFilter(user => user.Id == userId);
            var user = users.FirstOrDefault();
            if (user is null)
                return new RefreshTokenResponse { Message = "User not found", Success = false };

            var refreshTokens = await _refreshTokenRepository.FindByFilter(token => token.UserId == userId);
            var matchingToken = refreshTokens.FirstOrDefault(token => IsTokenMatch(token, req.RefreshToken));
            if (matchingToken is null || !matchingToken.IsActive)
                return new RefreshTokenResponse { Message = "Invalid refresh token", Success = false };

            var accessTokenResult = _tokenService.CreateAccessToken(user);
            var newRefreshTokenResult = _tokenService.CreateRefreshToken(user);

            matchingToken.Revoke("Rotated", newRefreshTokenResult.Token.Id);
            await _refreshTokenRepository.Update(matchingToken.Id.ToString(), matchingToken);
            await _refreshTokenRepository.Save(newRefreshTokenResult.Token);

            return new RefreshTokenResponse
            {
                Success = true,
                AccessToken = accessTokenResult.Token,
                RefreshToken = newRefreshTokenResult.RawToken,
                ExpiresAt = accessTokenResult.ExpiresAt,
                Message = "Token refreshed"
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while refreshing token.");
            return new RefreshTokenResponse { Message = "Refresh failed", Success = false };
        }
    }

    private bool IsTokenMatch(RefreshToken token, string rawToken)
    {
        return _hashingService.Verify(rawToken, token.TokenHash, token.Salt);
    }
}
