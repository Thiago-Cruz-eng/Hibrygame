using Orchestrator.Domain;

namespace Orchestrator.UseCases.Interfaces;

public interface IValidationService
{
    Task<bool> CreateValidation(ValidationDto req);

    /// <summary>Lanca <see cref="InvalidOperationException"/> quando nao existe validacao.</summary>
    Task<Validation> GetValidationByUserToken(string userId, string accessToken);

    Task<bool> UpdateValidationByUserToken(string userId, string accessToken, string pieceColor, string room);

    Task<bool> GetValidationCanMove(string userId, string token, string colorPiece, string room,
        string email, string day);

    // Removido daqui: GetValidationByUserIdTokenAndRoom, que era apenas
    // `throw new NotImplementedException()` e nao tinha um unico chamador. Ver DT-05.
}
