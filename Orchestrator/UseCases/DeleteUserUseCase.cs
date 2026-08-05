using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto.Response;

namespace Orchestrator.UseCases;

/// <summary>
/// Remove um usuário. Caso de uso de <c>DELETE /users/{id}</c>.
///
/// <para>
/// <b>A remoção é física e definitiva.</b> Não há exclusão lógica neste projeto — nenhum campo
/// "ativo" ou "removido em" —, e não há transação, então nada é desfeito. O documento sai da
/// coleção.
/// </para>
///
/// <para>
/// <b>O que fica para trás:</b> os refresh tokens do usuário <b>não</b> são removidos nem
/// revogados, e os registros de <c>Validation</c> também permanecem. Na prática, um refresh token
/// emitido antes da remoção continua ativo até expirar — e como <c>RefreshTokenUseCase</c> procura
/// o usuário antes de rotacionar, ele passa a devolver "User not found", o que resolve por
/// acidente e não por desenho. Se um dia a limpeza dessas coleções passar a importar, é aqui que
/// ela entra.
/// </para>
/// </summary>
public class DeleteUserUseCase
{
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly ILogger<DeleteUserUseCase> _logger;

    public DeleteUserUseCase(IUserRepositoryNoSql userRepository, ILogger<DeleteUserUseCase> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Remove o usuário de <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>Success = false</c> com <c>"User not found"</c> quando não existe, e com
    /// <c>"User not deleted"</c> quando existia na consulta mas a remoção não afetou documento
    /// nenhum — o que na prática significa que alguém o removeu no intervalo entre as duas
    /// operações.
    /// </returns>
    public async Task<DeleteUserResponse> DeleteAsync(string id)
    {
        try
        {
            // Busca antes de remover para poder distinguir "não existia" de "existia e a remoção
            // não pegou". Sem isso, as duas situações devolveriam a mesma mensagem.
            var users = await _userRepository.FindByFilter(user => user.Id.ToString() == id);
            var user = users.FirstOrDefault();
            if (user is null)
                return new DeleteUserResponse { Message = "User not found", Success = false };

            var deleted = await _userRepository.Delete(user.Id.ToString(), user);
            if (!deleted)
                return new DeleteUserResponse { Message = "User not deleted", Success = false };

            return new DeleteUserResponse { Message = "User deleted", Success = true };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while deleting user.");
            return new DeleteUserResponse { Message = "Same error happen", Success = false };
        }
    }
}
