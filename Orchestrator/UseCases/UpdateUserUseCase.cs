using Microsoft.Extensions.Logging;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Security.Authorization;

namespace Orchestrator.UseCases;

public class UpdateUserUseCase
{
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly ILogger<UpdateUserUseCase> _logger;

    public UpdateUserUseCase(IUserRepositoryNoSql userRepository, ILogger<UpdateUserUseCase> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<UpdateUserResponse> UpdateAsync(string id, UpdateUserRequest req)
    {
        try
        {
            var users = await _userRepository.FindByFilter(user => user.Id.ToString() == id);
            var user = users.FirstOrDefault();
            if (user is null)
                return new UpdateUserResponse { Message = "User not found", Success = false };

            var normalizedEmail = NormalizeEmail(req.Email);
            var existing = await _userRepository.FindByFilter(existingUser =>
                existingUser.Email == normalizedEmail && existingUser.Id != user.Id);
            if (existing.Any())
                return new UpdateUserResponse { Message = "Email already in use", Success = false };

            if (!RoleHierarchy.TryGetLevel(req.Role, out var roleLevel))
                return new UpdateUserResponse { Message = "Invalid role", Success = false };

            var normalizedRole = RoleHierarchy.NormalizeRole(roleLevel);
            var assignments = req.Assignments.Select(assignment => MapAssignment(assignment, req.ModifiedBy)).ToList();
            user.ChangeName(req.Name.Trim(), req.ModifiedBy.Trim())
                .ChangeEmail(normalizedEmail, req.ModifiedBy.Trim())
                .ChangeRole(normalizedRole, req.ModifiedBy.Trim())
                .ChangeAssignments(assignments, req.ModifiedBy.Trim());

            await _userRepository.Update(user.Id.ToString(), user);

            return new UpdateUserResponse { Message = "User updated", Success = true };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while updating user.");
            return new UpdateUserResponse { Message = "Same error happen", Success = false };
        }
    }

    private static UserAssignment MapAssignment(UserAssignmentDto assignment, string modifiedBy)
    {
        var nodes = assignment.HierarchyNodes
            .Select(node => UserAssignment.HierarchyNode.Create(node.NodeId, node.NodeName))
            .ToList();

        return UserAssignment.Create(
            assignment.TeamName,
            assignment.TeamId,
            assignment.RoleName,
            assignment.RoleId,
            nodes,
            modifiedBy);
    }

    private static string NormalizeEmail(string value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}
