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

    public UserController(CreateUserService createUserService, LoginAsyncUseCase loginAsync)
    {
        _createUserService = createUserService;
        _loginAsync = loginAsync;
    }

    [HttpPost("/login")]
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(LoginResponse))]
    public async Task<IActionResult> LoginUser(LoginRequest req)
    {
        var result = await _loginAsync.LoginAsync(req);
        return Ok(result);
    }
    
    [HttpPost("/create")]
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(LoginResponse))]
    public async Task<IActionResult> CreateUser(CreateUserRequest req)
    {
        var result = await _createUserService.CreateAsync(req);
        return Ok(result);
    }
}