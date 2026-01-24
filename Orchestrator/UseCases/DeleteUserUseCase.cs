using Microsoft.Extensions.Logging;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto.Response;

namespace Orchestrator.UseCases;

public class DeleteUserUseCase
{
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly ILogger<DeleteUserUseCase> _logger;

    public DeleteUserUseCase(IUserRepositoryNoSql userRepository, ILogger<DeleteUserUseCase> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<DeleteUserResponse> DeleteAsync(string id)
    {
        try
        {
            var users = await _userRepository.FindByFilter(user => user.Id.ToString() == id);
            var user = users.FirstOrDefault();
            if (user is null)
                return new DeleteUserResponse { Message = "User not found", Success = false };

            _userRepository.Delete(user.Id.ToString(), user);
            return new DeleteUserResponse { Message = "User deleted", Success = true };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while deleting user.");
            return new DeleteUserResponse { Message = "Same error happen", Success = false };
        }
    }
}
