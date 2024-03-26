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
    
    [HttpGet("/get-validation")]
    public async Task<IActionResult> GetValidation(string userId, string token)
    {
        var result = await _validationService.GetValidationByUserIdTokenAndRoom(userId, null, token);
        return Ok(result);
    }
    
    [HttpGet("/verify-validation")]
    public async Task<IActionResult> VerifyValidation(string userId, string token)
    {
        var result = await _validationService.GetValidationByUserToken(userId, token);
        return Ok(result);
    }
}