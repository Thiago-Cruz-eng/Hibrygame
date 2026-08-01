using System.IdentityModel.Tokens.Jwt;
﻿using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Security.Authorization;

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
    private readonly RegisterUserUseCase _registerUserUseCase;

    public UserController(
        CreateUserUseCase createUserUseCase,
        GetUserUseCase getUserUseCase,
        LoginAsyncUseCase loginAsync,
        UpdateUserUseCase updateUserUseCase,
        DeleteUserUseCase deleteUserUseCase,
        ChangePasswordUseCase changePasswordUseCase,
        RefreshTokenUseCase refreshTokenUseCase,
        RegisterUserUseCase registerUserUseCase)
    {
        _createUserUseCase = createUserUseCase;
        _getUserUseCase = getUserUseCase;
        _loginAsync = loginAsync;
        _updateUserUseCase = updateUserUseCase;
        _deleteUserUseCase = deleteUserUseCase;
        _changePasswordUseCase = changePasswordUseCase;
        _refreshTokenUseCase = refreshTokenUseCase;
        _registerUserUseCase = registerUserUseCase;
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

    /// <summary>
    /// Auto-registro. Papel e autor sao decididos no servidor: o corpo do request nao tem
    /// como pedir "super adm". Devolve sessao pronta, para o cliente nao precisar fazer
    /// login logo depois.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("/register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var result = await _registerUserUseCase.RegisterAsync(req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Criacao administrativa: aqui o papel PODE vir pelo corpo, e por isso o endpoint
    /// exige Role:Admin.
    ///
    /// Era [AllowAnonymous], o que permitia a qualquer pessoa na internet criar um
    /// "super adm" e depois apagar usuarios (DT-04). Quem quer apenas uma conta de
    /// jogador usa POST /register.
    ///
    /// Duas travas alem da politica: CreatedBy vem do claim `sub`, nunca do corpo, e
    /// ninguem cria papel acima do proprio nivel.
    /// </summary>
    [Authorize(Policy = "Role:Admin")]
    [HttpPost("/users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest req)
    {
        var callerId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(callerId)) return Forbid();

        if (!TryGetCallerRoleLevel(out var callerLevel)) return Forbid();

        if (!RoleHierarchy.TryGetLevel(req.Role ?? string.Empty, out var requestedLevel))
            return BadRequest(new CreateUserResponse { Success = false, Message = "Invalid role" });

        if (requestedLevel > callerLevel)
        {
            return BadRequest(new CreateUserResponse
            {
                Success = false,
                Message = "Cannot create a user with a role above your own."
            });
        }

        // Auditoria vem do token, nao do corpo: CreatedBy era forjavel.
        req.CreatedBy = callerId;

        var result = await _createUserUseCase.CreateAsync(req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private bool TryGetCallerRoleLevel(out RoleLevel level)
    {
        level = default;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        return !string.IsNullOrEmpty(role) && RoleHierarchy.TryGetLevel(role, out level);
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
