using AutoMapper;
using Orchestrator.Domain;
using Orchestrator.UseCases.Dto;
using Orchestrator.UseCases.Dto.Response;

namespace Orchestrator.UseCases.Profiles;

public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<CreateUserRequest, User>();
        CreateMap<UpdateUserRequest, User>();
    }
}