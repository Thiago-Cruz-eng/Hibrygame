using Orchestrator.Domain;
using Orchestrator.Infra.Mongo;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.UseCases;

public class ChangePasswordUseCase
{
    private readonly IGenericRepository _genericRepository;
    private readonly ISecureHashingService _hashingService;
    private readonly ILogger<ChangePasswordUseCase> _logger;

    public ChangePasswordUseCase(
        IGenericRepository genericRepository,
        ISecureHashingService hashingService,
        ILogger<ChangePasswordUseCase> logger)
    {
        _genericRepository = genericRepository;
        _hashingService = hashingService;
        _logger = logger;
    }

    public async Task<ChangePasswordResponse> ChangeAsync(ChangePasswordRequest req)
    {
        try
        {
            var user = await _genericRepository.GetFirstOrDefault<User>(user => user.Id.ToString() == req.UserId);
            if (user is null)
                return new ChangePasswordResponse { Message = "User not found", Success = false };

            if (!_hashingService.Verify(req.CurrentPassword, user.PasswordHash, user.Salt))
                return new ChangePasswordResponse { Message = "Invalid credentials", Success = false };
            
            var (hash, salt) = _hashingService.HashValue(req.NewPassword);
            user.ChangePassword(hash, salt, req.ModifiedBy.Trim());
            await _genericRepository.Update<User>(us => us.Id == user.Id, 
                CancellationToken.None, 
                (x => x.PasswordHash, user.PasswordHash),
                (x => x.Salt, user.Salt),
                (x => x.MustChangePassword, user.MustChangePassword),
                (x => x.ModificationInformations, user.ModificationInformations)
                );

            return new ChangePasswordResponse { Message = "Password updated", Success = true };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while changing password.");
            return new ChangePasswordResponse { Message = "Same error happen", Success = false };
        }
    }
}
