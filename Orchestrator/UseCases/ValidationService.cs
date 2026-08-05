using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.UseCases;

/// <summary>
/// Implementação de <see cref="IValidationService"/>.
///
/// <para>
/// <b>Leia a nota da interface antes de mexer aqui</b> — este subdomínio tem uma decisão humana
/// pendente sobre continuar existindo.
/// </para>
///
/// <para>
/// <b>Nota de nomenclatura:</b> a classe se chama <c>...Service</c> e não <c>...UseCase</c>, e
/// isso é intencional. Um caso de uso é uma ação (<c>CreateUserUseCase</c>); aqui há quatro
/// operações relacionadas ao mesmo registro, consumidas por outros casos de uso e pelo controller
/// — daí a forma de serviço, com interface, prevista na tabela de nomenclatura do
/// <c>AGENTS.md</c>.
/// </para>
/// </summary>
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

    /// <inheritdoc />
    public async Task<bool> CreateValidation(ValidationDto req)
    {
        try
        {
            // Inicializador de objeto, e não um Create(...): Validation não segue o contrato de
            // entidade do projeto. Está anotado na própria classe.
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

    /// <inheritdoc />
    public async Task<bool> GetValidationCanMove(string userId, string token, string colorPiece, string room)
    {
        try
        {
            // As quatro condições são combinadas com AND. Já foram, em versão anterior, combinadas
            // com OR — e um OR aqui aceitava o token de um usuário para a sala de outro. Se algum
            // dia precisar mexer neste filtro, é este o erro a não repetir.
            var validation = await _validationRepositoryNoSql.FindByFilter(x =>
                x.AcessToken == token &&
                x.UserId == userId &&
                x.Room == room &&
                x.PieceColor == colorPiece);

            return validation.FirstOrDefault() is not null;
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Erro ao verificar permissao de lance do usuario {UserId} na sala {Room}", userId, room);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<Validation> GetValidationByUserToken(string userId, string accessToken)
    {
        // Sem try/catch, diferente dos outros três métodos: este é o único que comunica ausência
        // por exceção. Ver a nota na interface.
        var validation = await _validationRepositoryNoSql.FindByFilter(
            x => x.AcessToken == accessToken && x.UserId == userId);

        return validation.FirstOrDefault()
               ?? throw new InvalidOperationException(
                   $"Nenhuma validacao encontrada para o usuario '{userId}' com o token informado.");
    }

    /// <inheritdoc />
    public async Task<bool> UpdateValidationByUserToken(string userId, string accessToken, string pieceColor, string room)
    {
        try
        {
            // A InvalidOperationException de GetValidationByUserToken é capturada pelo catch abaixo
            // e virada em `false`. É por isso que este método não propaga: "não existe autorização"
            // e "falhou ao atualizar" são a mesma resposta para quem chama.
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

/// <summary>
/// Dados para criar uma autorização de sessão. Transporte entre <c>LoginAsyncUseCase</c> e
/// <see cref="ValidationService"/>.
///
/// <para>
/// Não é DTO de API — não trafega em request nem em response, e por isso não mora em
/// <c>Dto/Request</c> ou <c>Dto/Response</c>.
/// </para>
/// </summary>
public class ValidationDto
{
    /// <summary>
    /// O access token <b>em claro</b>. O nome tem o mesmo erro de digitação da propriedade em
    /// <see cref="Validation"/>, e por lá está a explicação de por que não foi corrigido.
    /// </summary>
    public string AcessToken { get; set; } = null!;

    /// <summary>Sala. <c>null</c> no login, preenchida depois.</summary>
    public string? Room { get; set; }

    /// <summary>Id do usuário, como texto.</summary>
    public string UserId { get; set; } = null!;

    /// <summary>Cor autorizada. <c>null</c> no login.</summary>
    public string? PieceColor { get; set; }

    /// <summary>E-mail do usuário. Gravado, mas nenhum filtro o usa — DT-20.</summary>
    public string UserEmail { get; set; } = null!;
}
