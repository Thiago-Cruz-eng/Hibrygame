using AutoMapper;
using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto;
using Orchestrator.UseCases.Dto.Response;

namespace Orchestrator.UseCases;

public class CreateUserService
{
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly IMapper _mapper;

    public CreateUserService(
        IUserRepositoryNoSql userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
    }

    public async Task<List<User>> GetAsync() =>
        await _userRepository.FindAll();

    public async Task<User?> GetAsync(string id) =>
        await _userRepository.FindById(id);

    public async Task CreateAsync(CreateUserRequest newContract)
    {
        var contract = _mapper.Map<User>(newContract);
        await _userRepository.Save(contract);
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