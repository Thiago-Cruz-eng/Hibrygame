using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.Presentation;

[ApiController]
[Authorize(Policy = "Role:Player")]
[Route("validation")]
public class ValidationController : ControllerBase
{
    private readonly IValidationService _validationService;

    public ValidationController(IValidationService validationService)
    {
        _validationService = validationService;
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyValidationRequest req)
    {
        if (!IsCallerAuthorizedFor(req.UserId, out var token))
            return Forbid();

        var validation = await _validationService.GetValidationByUserToken(req.UserId, token);
        return Ok(new VerifyValidationResponse { Valid = validation is not null });
    }

    [HttpPost("get")]
    public async Task<IActionResult> Get([FromBody] GetValidationRequest req)
    {
        if (!IsCallerAuthorizedFor(req.UserId, out var token))
            return Forbid();

        var validation = await _validationService.GetValidationByUserToken(req.UserId, token);
        if (validation is null)
            return NotFound();

        return Ok(new GetValidationResponse
        {
            Id = validation.Id.ToString(),
            UserId = validation.UserId,
            UserEmail = validation.UserEmail,
            Room = validation.Room,
            PieceColor = validation.PieceColor,
            DayOfGame = validation.DayOfGame
        });
    }

    [HttpPost("update/{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateValidationRequest req)
    {
        if (!IsCallerAuthorizedFor(req.UserId, out var token))
            return Forbid();

        var updated = await _validationService.UpdateValidationByUserToken(
            req.UserId, token, req.PieceColor, req.Room);

        return Ok(new UpdateValidationResponse { Updated = updated });
    }

    [HttpPost("can-move")]
    public async Task<IActionResult> CanMove([FromBody] CanMoveValidationRequest req)
    {
        if (!IsCallerAuthorizedFor(req.UserId, out var token))
            return Forbid();

        // req.UserEmail e req.Day continuam no corpo do request (contrato de FE), mas
        // nunca chegavam ao filtro — os dois eram descartados dentro do servico. Ver DT-20.
        var canMove = await _validationService.GetValidationCanMove(
            req.UserId, token, req.PieceColor, req.Room);

        return Ok(new CanMoveValidationResponse { CanMove = canMove });
    }

    private bool IsCallerAuthorizedFor(string requestUserId, out string token)
    {
        token = string.Empty;
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(sub) || !string.Equals(sub, requestUserId, StringComparison.Ordinal))
            return false;

        var raw = HttpContext.GetTokenAsync("access_token").GetAwaiter().GetResult();
        if (string.IsNullOrEmpty(raw))
            return false;

        token = raw;
        return true;
    }
}
