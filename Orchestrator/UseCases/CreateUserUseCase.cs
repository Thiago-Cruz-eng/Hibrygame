using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;

namespace Orchestrator.UseCases;

public class CreateUserUseCase
{
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ILogger<CreateUserUseCase> _logger;

    public CreateUserUseCase(
        IUserRepositoryNoSql userRepository,
        IPasswordHasher<User> passwordHasher,
        ILogger<CreateUserUseCase> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<CreateUserResponse> CreateAsync(CreateUserRequest req)
    {
        try
        {
            var normalizedEmail = NormalizeValue(req.Email);
            var normalizedUserName = NormalizeValue(req.UserName);
            var existing = await _userRepository.FindByFilter(user =>
                user.NormalizedEmail == normalizedEmail || user.Email == req.Email);
            if (existing.Any())
                return new CreateUserResponse { Message = "User already has a account", Success = false };

            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = req.UserName?.Trim(),
                NormalizedUserName = normalizedUserName,
                Email = req.Email?.Trim(),
                NormalizedEmail = normalizedEmail,
                PhoneNumber = req.PhoneNumber?.Trim(),
                DateBirth = req.DateBirth ?? default,
                CreateAt = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString(),
                Address = string.Empty
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, req.Password);
            await _userRepository.Save(user);

            return new CreateUserResponse
            {
                Success = true,
                Message = "User create"
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while creating user.");
            return new CreateUserResponse { Message = "Same error happen", Success = false };
        }
    }

    private static string NormalizeValue(string value)
    {
        return value?.Trim().ToUpperInvariant() ?? string.Empty;
    }
}
