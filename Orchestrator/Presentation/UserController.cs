using Microsoft.AspNetCore.Mvc;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Dto;

namespace Orchestrator.Presentation;

[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    private readonly CreateUserService _createUserService;

    public UserController(CreateUserService createUserService)
    {
        _createUserService = createUserService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserRequest user)
    {
        await _createUserService.CreateAsync(user);
        return Ok();
    }
}