using Orchestrator.Domain;

namespace Orchestrator.UseCases.Interfaces;

public interface IValidationService
{
    Task<bool> CreateValidation(ValidationDto req);
    Task<bool> GetValidationByUserIdTokenAndRoom(string userId, string room, string accessToken);
    Task<Validation> GetValidationByUserToken(string userId, string accessToken);
    Task<bool> UpdateValidationByUserToken(string userId, string accessToken, string pieceColor, string room);

    Task<bool> GetValidationCanMove(string userId, string token, string colorPiece, string room,
        string email, string day);
}