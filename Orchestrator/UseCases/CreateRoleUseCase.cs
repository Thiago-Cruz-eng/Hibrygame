using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Orchestrator.Domain;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;

namespace Orchestrator.UseCases;

public class CreateRoleUseCase
{
    private readonly RoleManager<Roles> _roleManager;
    private readonly IMapper _mapper;

    public CreateRoleUseCase(RoleManager<Roles> roleManager, IMapper mapper)
    {
        _roleManager = roleManager;
        _mapper = mapper;
    }

    public async Task<CreateRoleResponse> CreateAsync(CreateRoleRequest req)
    {
        try
        {
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
            Console.WriteLine(e.Message);
            return new CreateRoleResponse{ Message = "Same error happen", Success = false};
        }
    }
}