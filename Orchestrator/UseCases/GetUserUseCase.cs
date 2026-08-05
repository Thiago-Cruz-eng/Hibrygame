using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto;
using Orchestrator.UseCases.Dto.Response;

namespace Orchestrator.UseCases;

/// <summary>
/// Lê um usuário pelo id. Caso de uso de <c>GET /users/{id}</c>.
///
/// <para>
/// <b>Exceção ao padrão do projeto:</b> este é o único caso de uso que <b>não</b> devolve um
/// <c>{Ação}Response</c> com <c>Success</c>. Ele devolve <c>GetUserResponse?</c> — o usuário, ou
/// <c>null</c>. É leitura: não há mais nada a comunicar além de "achei" ou "não achei", e o
/// controller traduz <c>null</c> em 404 direto.
/// </para>
///
/// <para>
/// <b>Cuidado ao mexer no que é devolvido.</b> A resposta é montada campo a campo, e é isso que
/// mantém <c>PasswordHash</c> e <c>Salt</c> fora dela. Trocar esta montagem por devolver a
/// entidade inteira exporia os dois numa resposta HTTP.
/// </para>
/// </summary>
public class GetUserUseCase
{
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly ILogger<GetUserUseCase> _logger;

    public GetUserUseCase(IUserRepositoryNoSql userRepository, ILogger<GetUserUseCase> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// O usuário de <paramref name="id"/>, ou <c>null</c>.
    /// </summary>
    /// <returns>
    /// <c>null</c> em <b>dois</b> casos diferentes que a assinatura não distingue: o usuário não
    /// existe, ou houve falha ao consultar. Os dois viram 404 no controller. A diferença fica
    /// apenas no log — se um usuário reclamar de "não encontrado" sem motivo, é lá que se procura.
    /// </returns>
    public async Task<GetUserResponse?> GetAsync(string id)
    {
        try
        {
            var userList = await _userRepository.FindByFilter(user => user.Id.ToString() == id);
            var user = userList.FirstOrDefault();
            if (user is null)
                return null;

            // Montagem explícita — ver a nota da classe sobre por que não devolver a entidade.
            return new GetUserResponse
            {
                Id = user.Id.ToString(),
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                MustChangePassword = user.MustChangePassword,
                Assignments = user.Assignments.Select(UserAssignmentDto.FromDomain).ToList()
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while retrieving user.");
            return null;
        }
    }
}
