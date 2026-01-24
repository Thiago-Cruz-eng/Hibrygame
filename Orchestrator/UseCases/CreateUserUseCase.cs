using Microsoft.Extensions.Logging;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Interfaces;
using Orchestrator.UseCases.Security.Authorization;

namespace Orchestrator.UseCases;

public class CreateUserUseCase
{
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly ISecureHashingService _hashingService;
    private readonly ILogger<CreateUserUseCase> _logger;

    public CreateUserUseCase(
        IUserRepositoryNoSql userRepository,
        ISecureHashingService hashingService,
        ILogger<CreateUserUseCase> logger)
    {
        _userRepository = userRepository;
        _hashingService = hashingService;
        _logger = logger;
    }

    public async Task<CreateUserResponse> CreateAsync(CreateUserRequest req)
    {
        try
        {
            var normalizedEmail = NormalizeEmail(req.Email);
            var existing = await _userRepository.FindByFilter(user => user.Email == normalizedEmail);
            if (existing.Any())
                return new CreateUserResponse { Message = "User already has a account", Success = false };

            if (!RoleHierarchy.TryGetLevel(req.Role, out var roleLevel))
                return new CreateUserResponse { Message = "Invalid role", Success = false };

            var normalizedRole = RoleHierarchy.NormalizeRole(roleLevel);
            var (hash, salt) = _hashingService.HashValue(req.Password);
            var assignments = req.Assignments
                .Select(assignment => MapAssignment(assignment, req.CreatedBy))
                .ToList();
            var user = User.Create(
                name: req.Name.Trim(),
                email: normalizedEmail,
                role: normalizedRole,
                passwordHash: hash,
                salt: salt,
                assignments: assignments,
                createdBy: req.CreatedBy.Trim());

            await _userRepository.Save(user);

            return new CreateUserResponse
            {
                Success = true,
                Message = "User created",
                UserId = user.Id.ToString()
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while creating user.");
            return new CreateUserResponse { Message = "Same error happen", Success = false };
        }
    }

    private static UserAssignment MapAssignment(UserAssignmentDto assignment, string createdBy)
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
            createdBy: createdBy);
    }

    private static string NormalizeEmail(string value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}
