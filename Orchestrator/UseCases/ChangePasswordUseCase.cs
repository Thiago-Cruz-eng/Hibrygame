using Microsoft.Extensions.Logging;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.UseCases;

public class ChangePasswordUseCase
{
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly ISecureHashingService _hashingService;
    private readonly ILogger<ChangePasswordUseCase> _logger;

    public ChangePasswordUseCase(
        IUserRepositoryNoSql userRepository,
        ISecureHashingService hashingService,
        ILogger<ChangePasswordUseCase> logger)
    {
        _userRepository = userRepository;
        _hashingService = hashingService;
        _logger = logger;
    }

    public async Task<ChangePasswordResponse> ChangeAsync(ChangePasswordRequest req)
    {
        try
        {
            var users = await _userRepository.FindByFilter(user => user.Id.ToString() == req.UserId);
            var user = users.FirstOrDefault();
            if (user is null)
                return new ChangePasswordResponse { Message = "User not found", Success = false };

            if (!_hashingService.Verify(req.CurrentPassword, user.PasswordHash, user.Salt))
                return new ChangePasswordResponse { Message = "Invalid credentials", Success = false };

            var (hash, salt) = _hashingService.HashValue(req.NewPassword);
            user.ChangePassword(hash, salt, req.ModifiedBy.Trim());
            await _userRepository.Update(user.Id.ToString(), user);

            return new ChangePasswordResponse { Message = "Password updated", Success = true };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while changing password.");
            return new ChangePasswordResponse { Message = "Same error happen", Success = false };
        }
    }
}
