using Orchestrator.Domain;

namespace Orchestrator.UseCases.Interfaces;

public interface IValidationService
{
    Task<bool> CreateValidation(ValidationDto req);

    /// <summary>Lanca <see cref="InvalidOperationException"/> quando nao existe validacao.</summary>
    Task<Validation> GetValidationByUserToken(string userId, string accessToken);

    Task<bool> UpdateValidationByUserToken(string userId, string accessToken, string pieceColor, string room);

    Task<bool> GetValidationCanMove(string userId, string token, string colorPiece, string room);

    // Removidos daqui:
    //   GetValidationByUserIdTokenAndRoom  era apenas `throw new NotImplementedException()`
    //                                      e nao tinha um unico chamador
    //   os parametros `email` e `day`       de GetValidationCanMove: eram recebidos e
    //                                      descartados, com os filtros comentados no
    //                                      corpo. Ver DT-05 e DT-20.
}
