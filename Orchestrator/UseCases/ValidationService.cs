using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.UseCases;

public class ValidationService : IValidationService
{
    private readonly IValidationRepositoryNoSql _validationRepositoryNoSql;

    public ValidationService(IValidationRepositoryNoSql validationRepositoryNoSql)
    {
        _validationRepositoryNoSql = validationRepositoryNoSql;
    }

    public async Task<bool> CreateValidation(ValidationDto req)
    {
        try
        {
            var validation = new Validation
            {
                AcessToken = req.AcessToken,
                Room = req.Room,
                UserId = req.UserId,
                PieceColor = req.PieceColor,
                UserEmail = req.UserEmail
            }; 
            await _validationRepositoryNoSql.Save(validation);
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }

    public Task<bool> GetValidationByUserIdTokenAndRoom(string userId, string room, string accessToken)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> GetValidationCanMove(string userId, string token, string colorPiece, string room, string email, string day)
    {
        try
        {
            var validation = await _validationRepositoryNoSql.FindByFilter(x => 
                x.AcessToken == token &&
                x.UserId == userId &&
                x.Room == room && 
                x.PieceColor == colorPiece 
                //x.UserEmail == email &&
                //x.DayOfGame == dayGame.ToUniversalTime()
                );
            _ = (validation.FirstOrDefault() ?? null) ?? throw new InvalidOperationException();
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }
    
    public async Task<bool> GetValidationCanMove(string userId, string room, string accessToken)
    {
        try
        {
            var validation = await _validationRepositoryNoSql.FindByFilter(x => x.AcessToken == accessToken ||
                (x.UserId == userId &&
                 x.Room == room));
            _ = (validation.FirstOrDefault() ?? null) ?? throw new InvalidOperationException();
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }
    
    public async Task<Validation> GetValidationByUserToken(string userId, string accessToken)
    {
        try
        {
            var validation = await _validationRepositoryNoSql.FindByFilter(x => x.AcessToken == accessToken && x.UserId == userId );
            _ = (validation.FirstOrDefault() ?? null) ?? throw new InvalidOperationException();
            return validation.FirstOrDefault();
        }
        catch (Exception e)
        {
            throw;
        }
    }

    public async Task<bool> UpdateValidationByUserToken(string userId, string accessToken, string pieceColor, string room)
    {
        try
        {
            var validation = await GetValidationByUserToken(userId, accessToken);
            validation.Room = room;
            validation.PieceColor = pieceColor;
            await _validationRepositoryNoSql.Update(validation.Id.ToString(), validation);
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }
}

public class ValidationDto
{
    public string AcessToken { get; set; } = null!;
    public string? Room { get; set; }
    public string UserId { get; set; } = null!;
    public string? PieceColor { get; set; }
    public string UserEmail { get; set; } = null!;
}
