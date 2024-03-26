using System.Net;
using Microsoft.AspNetCore.Mvc;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Dto;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;

namespace Orchestrator.Presentation;

[ApiController]
[Route("api/v1/[controller]")]
public class UserController : ControllerBase
{
    private readonly LoginAsyncUseCase _loginAsync;
    private readonly CreateUserService _createUserService;
    private readonly CreateRoleUseCase _createRole;

    public UserController(CreateUserService createUserService, LoginAsyncUseCase loginAsync, CreateRoleUseCase createRole)
    {
        _createUserService = createUserService;
        _loginAsync = loginAsync;
        _createRole = createRole;
    }

    [HttpPost("/login")]
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(LoginResponse))]
    public async Task<IActionResult> LoginUser(LoginRequest req)
    {
        var result = await _loginAsync.LoginAsync(req);
        return Ok(result);
    }
    
    [HttpPost("/create")]
    public async Task<IActionResult> CreateUser(CreateUserRequest req)
    {
        var result = await _createUserService.CreateAsync(req);
        return Ok(result);
    }
    
    [HttpGet("/get/{id}")]
    public async Task<IActionResult> GetUser(string id)
    {
        var result = await _createUserService.GetAsync(id);
        return Ok(result);
    }
    
    [HttpPost("/create-role")]
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(LoginResponse))]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest req)
    {
        var result = await _createRole.CreateAsync(req);
        return Ok(result);
    }
}