using Microsoft.Extensions.Logging;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.UseCases;

public class LoginAsyncUseCase
{
    private readonly IValidationService _validationService;
    private readonly ILogger<LoginAsyncUseCase> _logger;
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly IRefreshTokenRepositoryNoSql _refreshTokenRepository;
    private readonly ISecureHashingService _hashingService;
    private readonly ITokenService _tokenService;

    public LoginAsyncUseCase(
        IValidationService validationService,
        ILogger<LoginAsyncUseCase> logger,
        IUserRepositoryNoSql userRepository,
        IRefreshTokenRepositoryNoSql refreshTokenRepository,
        ISecureHashingService hashingService,
        ITokenService tokenService)
    {
        _validationService = validationService;
        _logger = logger;
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _hashingService = hashingService;
        _tokenService = tokenService;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest req)
    {
        try
        {
            var normalizedEmail = NormalizeEmail(req.Email);
            var users = await _userRepository.FindByFilter(user => user.Email == normalizedEmail);
            var user = users.FirstOrDefault();
            if (user is null)
                return new LoginResponse { Message = "Invalid credentials", Success = false };

            if (!_hashingService.Verify(req.Password, user.PasswordHash, user.Salt))
                return new LoginResponse { Message = "Invalid credentials", Success = false };

            var accessTokenResult = _tokenService.CreateAccessToken(user);
            var refreshTokenResult = _tokenService.CreateRefreshToken(user);
            await _refreshTokenRepository.Save(refreshTokenResult.Token);

            await _validationService.CreateValidation(new ValidationDto
            {
                AcessToken = accessTokenResult.Token,
                Room = null,
                UserId = user.Id.ToString(),
                PieceColor = null,
                UserEmail = user.Email
            });

            return new LoginResponse
            {
                Success = true,
                AccessToken = accessTokenResult.Token,
                RefreshToken = refreshTokenResult.RawToken,
                ExpiresAt = accessTokenResult.ExpiresAt,
                Email = user.Email,
                UserId = user.Id.ToString(),
                Role = user.Role,
                MustChangePassword = user.MustChangePassword,
                Message = "User logged"
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while logging user in.");
            return new LoginResponse { Message = "Login failed", Success = false };
        }
    }

    private static string NormalizeEmail(string value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}
