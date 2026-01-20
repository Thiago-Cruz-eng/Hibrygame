using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.Infra.Settings;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.UseCases;

public class LoginAsyncUseCase
{
    private readonly IValidationService _validationService;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<LoginAsyncUseCase> _logger;
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly IPasswordHasher<User> _passwordHasher;

    public LoginAsyncUseCase(
        IValidationService validationService,
        IOptions<JwtSettings> jwtOptions,
        ILogger<LoginAsyncUseCase> logger,
        IUserRepositoryNoSql userRepository,
        IPasswordHasher<User> passwordHasher)
    {
        _validationService = validationService;
        _jwtSettings = jwtOptions.Value;
        _logger = logger;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest req)
    {
        try
        {
            var normalizedEmail = NormalizeValue(req.Email);
            var users = await _userRepository.FindByFilter(user =>
                user.NormalizedEmail == normalizedEmail || user.Email == req.Email);
            var user = users.FirstOrDefault();
            if (user is null)
                return new LoginResponse { Message = "Invalid credentials", Success = false };

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
                return new LoginResponse { Message = "Invalid credentials", Success = false };

            var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
            if (verification == PasswordVerificationResult.Failed)
                return new LoginResponse { Message = "Invalid credentials", Success = false };
            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, req.Password);
                await _userRepository.Update(user.Id.ToString(), user);
            }

            var claims = new List<Claim>()
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiresMinutes);
            

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: expires,
                signingCredentials: credentials
            );

            await _validationService.CreateValidation(new ValidationDto
            {
                AcessToken = new JwtSecurityTokenHandler().WriteToken(token),
                Room = null,
                UserId = user.Id.ToString(),
                PieceColor = null,
                UserEmail = user.Email
            });

            return new LoginResponse
            {
                Success = true,
                AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
                Email = user.Email,
                UserId = user.Id.ToString(),
                Message = "User loged"
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while logging user in.");
            return new LoginResponse { Message = "Login failed", Success = false };
        }
    }

    private static string NormalizeValue(string value)
    {
        return value?.Trim().ToUpperInvariant() ?? string.Empty;
    }
}
