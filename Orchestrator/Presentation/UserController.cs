using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;

namespace Orchestrator.Presentation;

[ApiController]
[Route("api/v1/[controller]")]
public class UserController : ControllerBase
{
    private readonly LoginAsyncUseCase _loginAsync;
    private readonly CreateUserUseCase _createUserUseCase;
    private readonly GetUserUseCase _getUserUseCase;
    private readonly UpdateUserUseCase _updateUserUseCase;
    private readonly DeleteUserUseCase _deleteUserUseCase;
    private readonly ChangePasswordUseCase _changePasswordUseCase;
    private readonly RefreshTokenUseCase _refreshTokenUseCase;

    public UserController(
        CreateUserUseCase createUserUseCase,
        GetUserUseCase getUserUseCase,
        LoginAsyncUseCase loginAsync,
        UpdateUserUseCase updateUserUseCase,
        DeleteUserUseCase deleteUserUseCase,
        ChangePasswordUseCase changePasswordUseCase,
        RefreshTokenUseCase refreshTokenUseCase)
    {
        _createUserUseCase = createUserUseCase;
        _getUserUseCase = getUserUseCase;
        _loginAsync = loginAsync;
        _updateUserUseCase = updateUserUseCase;
        _deleteUserUseCase = deleteUserUseCase;
        _changePasswordUseCase = changePasswordUseCase;
        _refreshTokenUseCase = refreshTokenUseCase;
    }

    [AllowAnonymous]
    [HttpPost("/login")]
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(LoginResponse))]
    public async Task<IActionResult> LoginUser(LoginRequest req)
    {
        var result = await _loginAsync.LoginAsync(req);
        if (!result.Success)
        {
            return Unauthorized(result);
        }
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("/refresh-token")]
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(RefreshTokenResponse))]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest req)
    {
        var result = await _refreshTokenUseCase.RefreshAsync(req);
        if (!result.Success)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("/users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest req)
    {
        var result = await _createUserUseCase.CreateAsync(req);
        return Ok(result);
    }

    [Authorize(Policy = "Role:Player")]
    [HttpGet("/users/{id}")]
    public async Task<IActionResult> GetUser(string id)
    {
        var result = await _getUserUseCase.GetAsync(id);
        if (result is null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    [Authorize(Policy = "Role:TeamLeader")]
    [HttpPut("/users/{id}")]
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(UpdateUserResponse))]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest req)
    {
        var result = await _updateUserUseCase.UpdateAsync(id, req);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    [Authorize(Policy = "Role:Admin")]
    [HttpDelete("/users/{id}")]
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DeleteUserResponse))]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var result = await _deleteUserUseCase.DeleteAsync(id);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    [Authorize(Policy = "Role:Player")]
    [HttpPost("/users/change-password")]
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(ChangePasswordResponse))]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var result = await _changePasswordUseCase.ChangeAsync(req);
        if (!result.Success)
        {
            return Unauthorized(result);
        }
        return Ok(result);
    }
}
