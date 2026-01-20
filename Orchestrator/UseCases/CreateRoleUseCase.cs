using Microsoft.AspNetCore.Identity;
using Orchestrator.Domain;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;

namespace Orchestrator.UseCases;

public class CreateRoleUseCase
{
    private readonly RoleManager<Roles> _roleManager;
    private readonly ILogger<CreateRoleUseCase> _logger;

    public CreateRoleUseCase(RoleManager<Roles> roleManager, ILogger<CreateRoleUseCase> logger)
    {
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task<CreateRoleResponse> CreateAsync(CreateRoleRequest req)
    {
        try
        {
            var role = new Roles
            {
                Name = req.Name?.Trim(),
                NormalizedName = req.Name?.Trim().ToUpperInvariant()
            };
            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded)
                return new CreateRoleResponse { Message = $"Role not create {result.Errors.First().Description}", Success = false };
            
            var roleMap = _mapper.Map<Roles>(req);
            var result = await _roleManager.CreateAsync(roleMap);
            if(!result.Succeeded) return new CreateRoleResponse { Message = $"Role not create {result.Errors.First().Description}", Success = false };

            return new CreateRoleResponse
            {
                Message = "Role created",
                Success = true
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while creating role.");
            return new CreateRoleResponse { Message = "Same error happen", Success = false };
        }
    }
}
