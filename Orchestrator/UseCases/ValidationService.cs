using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.UseCases;

public class ValidationService : IValidationService
{
    private readonly IValidationRepositoryNoSql _validationRepositoryNoSql;
    private readonly ILogger<ValidationService> _logger;

    public ValidationService(
        IValidationRepositoryNoSql validationRepositoryNoSql,
        ILogger<ValidationService> logger)
    {
        _validationRepositoryNoSql = validationRepositoryNoSql;
        _logger = logger;
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
            _logger.LogError(e, "Erro ao criar validacao para o usuario {UserId}", req.UserId);
            return false;
        }
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
                // DECISAO PENDENTE: `email` e `day` sao recebidos e ignorados. Ou entram
                // no filtro, ou saem da assinatura. Ver DT-05 em docs/debito-tecnico.md.
                );

            return validation.FirstOrDefault() is not null;
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Erro ao verificar permissao de lance do usuario {UserId} na sala {Room}", userId, room);
            return false;
        }
    }

    public async Task<Validation> GetValidationByUserToken(string userId, string accessToken)
    {
        var validation = await _validationRepositoryNoSql.FindByFilter(
            x => x.AcessToken == accessToken && x.UserId == userId);

        return validation.FirstOrDefault()
               ?? throw new InvalidOperationException(
                   $"Nenhuma validacao encontrada para o usuario '{userId}' com o token informado.");
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
            _logger.LogError(e,
                "Erro ao atualizar validacao do usuario {UserId} para a sala {Room}", userId, room);
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
