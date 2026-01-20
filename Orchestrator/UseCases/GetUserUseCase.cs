using Microsoft.Extensions.Logging;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto.Response;

namespace Orchestrator.UseCases;

public class GetUserUseCase
{
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly ILogger<GetUserUseCase> _logger;

    public GetUserUseCase(IUserRepositoryNoSql userRepository, ILogger<GetUserUseCase> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<GetUserResponse?> GetAsync(string id)
    {
        try
        {
            var userList = await _userRepository.FindByFilter(user => user.Id.ToString() == id);
            var user = userList.FirstOrDefault();
            if (user is null)
                return null;

            return new GetUserResponse
            {
                UserName = user.UserName,
                Email = user.Email
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while retrieving user.");
            return null;
        }
    }
}
