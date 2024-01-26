using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;

namespace Orchestrator.UseCases;

public class CreateUserService
{
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly Microsoft.AspNetCore.Identity.UserManager<User> _userManager;
    private readonly IMapper _mapper;

    public CreateUserService(
        IUserRepositoryNoSql userRepository, IMapper mapper, UserManager<User> userManager)
    {
        _userRepository = userRepository;
        _mapper = mapper;
        _userManager = userManager;
    }

    public async Task<List<User>> GetAsync() =>
        await _userRepository.FindAll();

    public async Task<User?> GetAsync(string id) =>
        await _userRepository.FindById(id);

    public async Task<CreateUserResponse> CreateAsync(CreateUserRequest req)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(req.Email);
            if (user is null) return new CreateUserResponse { Message = "User not Found", Success = false };
            var userMap = _mapper.Map<User>(req);
            
            var userSaved = await _userManager.CreateAsync(userMap, userMap.Password);
            if(!userSaved.Succeeded) return new CreateUserResponse { Message = $"User not Found {userSaved.Errors.First().Description}", Success = false };

            var addRoleToUser = await _userManager.AddToRoleAsync(userMap, "USER");


        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return new CreateUserResponse{ Message = "Same error happen", Success = false};
        }
    }

    public async Task UpdateAsync(string id, UpdateUserRequest updatedContract)
    {
        var contract = _mapper.Map<User>(updatedContract);
        await _userRepository.Update(id, contract);
    }

    public async Task RemoveAsync(string id) {
        var contract = await _userRepository.FindById(id);
        _userRepository.Delete(id, contract);
    }
}