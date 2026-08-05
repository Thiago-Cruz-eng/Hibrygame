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
/// <b>Superfície de segurança (DT-16):</b> não confere quem está pedindo. Um usuário autenticado
/// pode alterar outro, inclusive promovê-lo, se alcançar o endpoint. Ver
/// <c>docs/debito-tecnico.md</c> antes de ampliar o que este caso de uso permite.
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
    /// <returns>
    /// <c>Success = false</c> quando o usuário não existe, quando o e-mail novo já pertence a
    /// outro, ou quando o papel é inválido.
    /// </returns>
    public async Task<UpdateUserResponse> UpdateAsync(string id, UpdateUserRequest req)
    {
        try
        {
            var users = await _userRepository.FindByFilter(user => user.Id.ToString() == id);
            var user = users.FirstOrDefault();
            if (user is null)
                return new UpdateUserResponse { Message = "User not found", Success = false };

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

            var normalizedRole = RoleHierarchy.NormalizeRole(roleLevel);
            var assignments = req.Assignments
                .Select(assignment => assignment.ToDomain(req.ModifiedBy))
                .ToList();

            // Mutadores encadeados — cada um devolve `this`. Cada chamada reescreve
            // ModificationInformations, então o que fica gravado é o autor da última; sendo o mesmo
            // `ModifiedBy` nas quatro, dá no mesmo.
            user.ChangeName(req.Name.Trim(), req.ModifiedBy.Trim())
                .ChangeEmail(normalizedEmail, req.ModifiedBy.Trim())
                .ChangeRole(normalizedRole, req.ModifiedBy.Trim())
                .ChangeAssignments(assignments, req.ModifiedBy.Trim());

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
