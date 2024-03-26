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

    public async Task<bool> GetValidationByUserIdTokenAndRoom(string userId, string room, string accessToken)
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
}

public class ValidationDto
{
    public string AcessToken { get; set; }
    public string Room { get; set; }
    public string UserId { get; set; }
    public string PieceColor { get; set; }
    public string UserEmail { get; set; }
}