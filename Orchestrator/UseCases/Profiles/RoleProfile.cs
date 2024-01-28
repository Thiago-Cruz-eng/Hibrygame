using AutoMapper;
using Orchestrator.Domain;
using Orchestrator.UseCases.Dto.Request;

namespace Orchestrator.UseCases.Profiles;

public class RoleProfile: Profile
{
    public RoleProfile()
    {
        CreateMap<CreateRoleRequest, Roles>();
    }
}