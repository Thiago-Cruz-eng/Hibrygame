using Microsoft.Extensions.Logging;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto;
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
                Id = user.Id.ToString(),
                Name = user.Name,
                Email = user.Email,
                MustChangePassword = user.MustChangePassword,
                Assignments = user.Assignments.Select(MapAssignment).ToList()
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while retrieving user.");
            return null;
        }
    }

    private static UserAssignmentDto MapAssignment(UserAssignment assignment)
    {
        return new UserAssignmentDto
        {
            TeamName = assignment.TeamName,
            TeamId = assignment.TeamId,
            RoleName = assignment.RoleName,
            RoleId = assignment.RoleId,
            HierarchyNodes = assignment.HierarchyNodes.Select(node => new HierarchyNodeDto
            {
                NodeId = node.NodeId,
                NodeName = node.NodeName
            }).ToList()
        };
    }
}
