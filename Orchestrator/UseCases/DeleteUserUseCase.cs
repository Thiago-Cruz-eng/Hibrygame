using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Security.Authorization;

namespace Orchestrator.UseCases;

/// <summary>
/// Remove um usuário e tudo o que dava acesso a ele. Caso de uso de <c>DELETE /users/{id}</c>.
///
/// <para>
/// <b>A remoção é física e definitiva.</b> Não há exclusão lógica neste projeto — nenhum campo
/// "ativo" ou "removido em" —, e não há transação, então nada é desfeito.
/// </para>
///
/// <para>
/// <b>Ninguém apaga quem está acima de si.</b> O nível do chamador chega por parâmetro (o
/// controller o lê do claim de papel) e é comparado com o papel atual do alvo. A policy do
/// endpoint garante apenas que o chamador é ao menos <c>adm</c>; ela não sabe quem ele está
/// tentando apagar — sem esta checagem, um <c>adm</c> apagava um <c>super adm</c>.
/// </para>
///
/// <para>
/// <b>A limpeza faz parte da remoção.</b> Refresh tokens e registros de <c>Validation</c> do
/// usuário eram deixados para trás: o refresh token continuava ativo até expirar e só não
/// funcionava porque <c>RefreshTokenUseCase</c> procura o usuário antes de rotacionar — resolvia
/// por acidente, não por desenho. Agora os tokens são revogados e as validações removidas aqui.
/// </para>
///
/// <para>
/// <b>Sem transação, a ordem importa.</b> O usuário sai primeiro; a limpeza vem depois. Se a
/// limpeza falhar no meio, sobra credencial órfã de um usuário que não existe mais — e credencial
/// órfã não autentica ninguém, porque a autenticação exige o usuário. A ordem inversa deixaria o
/// usuário vivo e sem sessão, que é pior.
/// </para>
/// </summary>
public class DeleteUserUseCase
{
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly IRefreshTokenRepositoryNoSql _refreshTokenRepository;
    private readonly IValidationRepositoryNoSql _validationRepository;
    private readonly ILogger<DeleteUserUseCase> _logger;

    public DeleteUserUseCase(
        IUserRepositoryNoSql userRepository,
        IRefreshTokenRepositoryNoSql refreshTokenRepository,
        IValidationRepositoryNoSql validationRepository,
        ILogger<DeleteUserUseCase> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _validationRepository = validationRepository;
        _logger = logger;
    }

    /// <summary>
    /// Remove o usuário de <paramref name="id"/>, se o chamador tiver alçada para isso.
    /// </summary>
    /// <param name="callerLevel">Nível hierárquico de quem pediu a remoção, lido do token.</param>
    /// <param name="callerId">Id de quem pediu, apenas para o registro de log.</param>
    /// <returns>
    /// <c>Success = false</c> com <c>"User not found"</c> quando não existe; com
    /// <c>"Cannot delete a user with a role above your own."</c> quando o alvo é mais graduado
    /// que o chamador; e com <c>"User not deleted"</c> quando existia na consulta mas a remoção
    /// não afetou documento nenhum — o que na prática significa que alguém o removeu no intervalo
    /// entre as duas operações.
    /// </returns>
    public async Task<DeleteUserResponse> DeleteAsync(string id, RoleLevel callerLevel, string callerId)
    {
        try
        {
            // Busca antes de remover para poder distinguir "não existia" de "existia e a remoção
            // não pegou" — e porque sem o usuário não há como saber o papel dele.
            var users = await _userRepository.FindByFilter(user => user.Id.ToString() == id);
            var user = users.FirstOrDefault();
            if (user is null)
                return new DeleteUserResponse { Message = "User not found", Success = false };

            if (RoleHierarchy.TryGetLevel(user.Role, out var targetLevel) && targetLevel > callerLevel)
            {
                _logger.LogWarning(
                    "User {CallerId} tried to delete user {TargetId}, who outranks them", callerId, user.Id);

                return new DeleteUserResponse
                {
                    Message = "Cannot delete a user with a role above your own.",
                    Success = false
                };
            }

            var deleted = await _userRepository.Delete(user.Id.ToString(), user);
            if (!deleted)
                return new DeleteUserResponse { Message = "User not deleted", Success = false };

            await PurgeCredentialsAsync(user.Id);

            // Quem apagou quem, com os dois ids. É o registro que uma investigação procura
            // primeiro, e remoção física não deixa outro rastro.
            _logger.LogInformation("User {TargetId} deleted by {CallerId}", user.Id, callerId);

            return new DeleteUserResponse { Message = "User deleted", Success = true };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while deleting user.");
            return new DeleteUserResponse { Message = "Same error happen", Success = false };
        }
    }

    /// <summary>
    /// Revoga os refresh tokens e remove os registros de validação do usuário removido.
    /// </summary>
    private async Task PurgeCredentialsAsync(Guid userId)
    {
        var tokens = await _refreshTokenRepository.FindByFilter(token => token.UserId == userId);

        // Revoga em vez de apagar: o token revogado é a evidência de que a sessão existiu e de
        // por que ela terminou, e é o que RefreshTokenUseCase consulta para detectar reuso.
        var revocations = tokens
            .Where(token => token.IsActive)
            .Select(token =>
            {
                token.Revoke("User deleted");
                return _refreshTokenRepository.Update(token.Id.ToString(), token);
            });

        // O id do usuário viaja como texto na coleção Validation — ver a nota em Validation.UserId.
        var userIdAsText = userId.ToString();
        var validations = await _validationRepository.FindByFilter(
            validation => validation.UserId == userIdAsText);

        // Validação, ao contrário do token, é apagada: ela não é evidência de nada, guarda o
        // resumo de um access token que já não vale e só serviria para crescer a coleção.
        var removals = validations.Select(validation =>
            _validationRepository.Delete(validation.Id.ToString(), validation));

        await Task.WhenAll(revocations.Concat<Task>(removals));
    }
}
