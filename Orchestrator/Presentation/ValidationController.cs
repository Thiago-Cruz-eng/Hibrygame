using Microsoft.AspNetCore.Mvc;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.Presentation;

[ApiController]
[Route("api/v1/[controller]")]
public class ValidationController : ControllerBase
{
    private readonly IValidationService _validationService;

    public ValidationController(IValidationService validationService)
    {
        _validationService = validationService;
    }

    [HttpPost("/generate-validation")]
    public async Task<IActionResult> GenerateValidation(ValidationDto req)
    {
        var result = await _validationService.CreateValidation(req);
        return Ok(result);
    }
    
    [HttpGet("/get-validation/{userId}/{token}")]
    public async Task<IActionResult> GetValidation(string userId, string token)
    {
        var result = await _validationService.GetValidationByUserToken(userId, token);
        return Ok(result);
    }
    
    [HttpGet("/get-validation-can-move/{userId}/{token}/{colorPiece}/{room}/{email}/{day}")]
    public async Task<IActionResult> GetValidationIfCanMove(string userId, string token, string colorPiece, string room, string email, string day)
    {
        var result = await _validationService.GetValidationCanMove(userId, token, colorPiece, room, email, day);
        return Ok(result);
    }
    
    [HttpGet("/verify-validation/{userId}/{token}")]
    public async Task<IActionResult> VerifyValidation(string userId, string token)
    {
        var result = await _validationService.GetValidationByUserToken(userId, token);
        return Ok(result);
    }
    
    [HttpGet("/update-validation/{userId}/{token}/{pieceColor}/{room}")]
    public async Task<IActionResult> UpdateValidation(string userId, string token, string pieceColor, string room)
    {
        var result = await _validationService.UpdateValidationByUserToken(userId, token, pieceColor, room);
        return Ok(result);
    }
}