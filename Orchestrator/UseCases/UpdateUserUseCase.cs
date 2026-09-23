using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Security.Authorization;

namespace Orchestrator.UseCases;

/// <summary>
/// Altera nome, e-mail, papel e vínculos de um usuário existente. Caso de uso de
/// <c>PUT /users/{id}</c>.
///
/// <para>
/// <b>Duas travas de alçada, e as duas são necessárias</b> (era a DT-16):
/// </para>
/// <list type="number">
///   <item><description>
///   <b>quem se altera</b> — o papel <b>atual</b> do alvo não pode estar acima do nível do
///   chamador. Sem isto, um <c>lider de time</c> (nível 3) edita um <c>adm</c> (nível 4) e pode
///   rebaixá-lo, trocar o e-mail dele e assumir a conta.
///   </description></item>
///   <item><description>
///   <b>o que se concede</b> — o papel <b>pedido</b> também não pode estar acima do nível do
///   chamador. Sem isto, qualquer um que alcance o endpoint se promove a <c>super adm</c>.
///   </description></item>
/// </list>
///
/// <para>
/// A checagem mora aqui, e não no controller, porque a primeira delas depende de <b>ler o
/// usuário</b> — o controller não consulta banco. O que vem do controller é só o nível do
/// chamador, que ele extrai do token.
/// </para>
///
/// <para>
/// <b>É substituição, não alteração parcial.</b> Todos os campos do request são aplicados: enviar
/// <c>Assignments</c> vazio <b>apaga</b> os vínculos do usuário. Não existe "mudar só o nome" por
/// aqui.
/// </para>
/// </summary>
public class UpdateUserUseCase
{
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly ILogger<UpdateUserUseCase> _logger;

    public UpdateUserUseCase(IUserRepositoryNoSql userRepository, ILogger<UpdateUserUseCase> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Aplica as alterações ao usuário de <paramref name="id"/>.
    /// </summary>
    /// <param name="id">
    /// Id do usuário, como texto — vem da rota. Id que não corresponde a ninguém devolve
    /// "User not found", não exceção.
    /// </param>
    /// <param name="req">
    /// Os campos novos. <c>ModifiedBy</c> é <b>ignorado</b>: a auditoria usa
    /// <paramref name="callerId"/>.
    /// </param>
    /// <param name="callerLevel">Nível hierárquico de quem pediu a alteração, lido do token.</param>
    /// <param name="callerId">Id de quem pediu. Vai para a auditoria e para o log.</param>
    /// <returns>
    /// <c>Success = false</c> quando o usuário não existe, quando o alvo ou o papel pedido estão
    /// acima da alçada do chamador, quando o e-mail novo já pertence a outro, ou quando o papel é
    /// inválido.
    /// </returns>
    public async Task<UpdateUserResponse> UpdateAsync(
        string id,
        UpdateUserRequest req,
        RoleLevel callerLevel,
        string callerId)
    {
        try
        {
            var users = await _userRepository.FindByFilter(user => user.Id.ToString() == id);
            var user = users.FirstOrDefault();
            if (user is null)
                return new UpdateUserResponse { Message = "User not found", Success = false };

            // Papel gravado que RoleHierarchy não reconhece não conta como nível nenhum — é a
            // mesma leitura que MinimumRoleHandler faz, onde papel escrito errado é negação
            // silenciosa. Tratá-lo como "acima de todos" travaria a correção do próprio registro.
            if (RoleHierarchy.TryGetLevel(user.Role, out var targetLevel) && targetLevel > callerLevel)
            {
                _logger.LogWarning(
                    "User {CallerId} tried to modify user {TargetId}, who outranks them", callerId, user.Id);

                return new UpdateUserResponse
                {
                    Message = "Cannot modify a user with a role above your own.",
                    Success = false
                };
            }

            var normalizedEmail = EmailNormalization.Normalize(req.Email);

            // `Id != user.Id` é o detalhe que faz esta checagem funcionar: sem ele, salvar o
            // usuário sem trocar o e-mail encontraria ele próprio e recusaria a alteração com
            // "Email already in use".
            var existing = await _userRepository.FindByFilter(existingUser =>
                existingUser.Email == normalizedEmail && existingUser.Id != user.Id);
            if (existing.Any())
                return new UpdateUserResponse { Message = "Email already in use", Success = false };

            if (!RoleHierarchy.TryGetLevel(req.Role, out var roleLevel))
                return new UpdateUserResponse { Message = "Invalid role", Success = false };

            // Depois de validar o papel, e não antes: trocar a ordem faria papel inexistente ser
            // reportado como "acima do seu nível", mandando quem depura para o lugar errado.
            if (roleLevel > callerLevel)
            {
                _logger.LogWarning(
                    "User {CallerId} tried to assign role {Role}, above their own level", callerId, roleLevel);

                return new UpdateUserResponse
                {
                    Message = "Cannot assign a role above your own.",
                    Success = false
                };
            }

            var normalizedRole = RoleHierarchy.NormalizeRole(roleLevel);

            // Autor da alteração vem do token. O ModifiedBy do corpo era forjável e foi descartado.
            var modifiedBy = callerId.Trim();

            var assignments = req.Assignments
                .Select(assignment => assignment.ToDomain(modifiedBy))
                .ToList();

            // Mutadores encadeados — cada um devolve `this`. Cada chamada reescreve
            // ModificationInformations, então o que fica gravado é o autor da última; sendo o mesmo
            // `modifiedBy` nas quatro, dá no mesmo.
            user.ChangeName(req.Name.Trim(), modifiedBy)
                .ChangeEmail(normalizedEmail, modifiedBy)
                .ChangeRole(normalizedRole, modifiedBy)
                .ChangeAssignments(assignments, modifiedBy);

            // Update aqui substitui o documento inteiro. É o que se quer neste caso de uso, já que
            // ele aplica todos os campos de uma vez.
            await _userRepository.Update(user.Id.ToString(), user);

            return new UpdateUserResponse { Message = "User updated", Success = true };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while updating user.");
            return new UpdateUserResponse { Message = "Same error happen", Success = false };
        }
    }
}
