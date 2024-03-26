using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNet.Identity;
using Microsoft.IdentityModel.Tokens;
using Orchestrator.Domain;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.UseCases;

public class LoginAsyncUseCase
{
    private readonly Microsoft.AspNetCore.Identity.UserManager<User> _userManager;
    private readonly IValidationService _validationService;

    public LoginAsyncUseCase(Microsoft.AspNetCore.Identity.UserManager<User> userManager, IValidationService validationService)
    {
        _userManager = userManager;
        _validationService = validationService;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest req)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(req.Email);
            if (user is null) return new LoginResponse { Message = "User not Found", Success = false };
            
            var isPasswordValid = await _userManager.CheckPasswordAsync(user, req.Password);
            if (!isPasswordValid)
                return new LoginResponse { Message = "Invalid password", Success = false };

            var claims = new List<Claim>()
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email)
            };
            var roles = await _userManager.GetRolesAsync(user);
            var roleClaims = roles.Select(x => new Claim(ClaimTypes.Role, x));
            claims.AddRange(roleClaims);

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("agbika7OUASHN*/**//+aicsdc89/aihs||oiihda"));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.Now.AddMinutes(60);
            

            var token = new JwtSecurityToken(
                // issuer: "",
                // audience: "",
                claims: claims,
                expires: expires,
                signingCredentials: credentials
            );

            _validationService.CreateValidation(new ValidationDto
            {
                AcessToken = new JwtSecurityTokenHandler().WriteToken(token),
                Room = null,
                UserId = user.Id.ToString(),
                PieceColor = null,
                UserEmail = user.Email
            });

            return new LoginResponse
            {
                Success = true,
                AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
                Email = user.Email,
                UserId = user.Id.ToString(),
                Message = "User loged"
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return new LoginResponse{ Message = "Same error happen", Success = false};
        }
    }
}