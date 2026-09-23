using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.UseCases;

/// <summary>
/// Troca a senha de um usuário que informou a senha atual corretamente.
///
/// <para>
/// <b>O alvo vem do token, não do corpo.</b> O controller preenche <c>userId</c> com o claim
/// <c>sub</c> de quem chamou e ignora o <c>UserId</c> do request. Era a DT-16: qualquer jogador
/// autenticado trocava a senha de qualquer usuário, bastando saber a senha atual dele.
/// </para>
///
/// <para>
/// <b>Trocar a senha encerra as outras sessões.</b> Todos os refresh tokens ativos do usuário são
/// revogados aqui. É o que dá sentido à troca de senha como resposta a um comprometimento: sem
/// isso, quem tivesse copiado um refresh token continuaria renovando o acesso por até 30 dias
/// depois de a vítima trocar a senha. O access token já emitido continua valendo até expirar
/// (no máximo 60 minutos) — não existe revogação de access token neste projeto.
/// </para>
/// </summary>
public class ChangePasswordUseCase
{
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly IRefreshTokenRepositoryNoSql _refreshTokenRepository;
    private readonly ISecureHashingService _hashingService;
    private readonly ILogger<ChangePasswordUseCase> _logger;

    public ChangePasswordUseCase(
        IUserRepositoryNoSql userRepository,
        IRefreshTokenRepositoryNoSql refreshTokenRepository,
        ISecureHashingService hashingService,
        ILogger<ChangePasswordUseCase> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _hashingService = hashingService;
        _logger = logger;
    }

    /// <summary>
    /// Confere a senha atual e, se ela bater, grava o hash da nova e derruba as sessões.
    /// </summary>
    /// <param name="userId">
    /// O dono da conta, derivado do claim <c>sub</c> pelo controller. <b>Nunca</b> o
    /// <c>UserId</c> do corpo do request.
    /// </param>
    /// <param name="req">
    /// O corpo do request. <c>UserId</c> e <c>ModifiedBy</c> são ignorados — ver
    /// <see cref="ChangePasswordRequest"/>.
    /// </param>
    public async Task<ChangePasswordResponse> ChangeAsync(string userId, ChangePasswordRequest req)
    {
        try
        {
            // Comparação direta por Guid, e não `Id.ToString() == id`: aquele deixa a tradução da
            // consulta a cargo do tradutor de expressão do driver (DT-17).
            if (!Guid.TryParse(userId, out var id))
                return new ChangePasswordResponse { Message = "User not found", Success = false };

            var user = await _userRepository.GetById(id);
            if (user is null)
                return new ChangePasswordResponse { Message = "User not found", Success = false };

            // Senha nunca é comparada em claro: `Verify` re-deriva o hash da senha informada
            // usando o salt guardado e compara os hashes.
            if (!_hashingService.Verify(req.CurrentPassword, user.PasswordHash, user.Salt))
            {
                // Sinal de segurança: troca de senha com a senha atual errada é o que uma tentativa
                // de tomada de conta produz. Só o id entra no log — nunca senha, nunca e-mail.
                _logger.LogWarning("Failed password change attempt for user {UserId}", id);
                return new ChangePasswordResponse { Message = "Invalid credentials", Success = false };
            }

            // Senha nova ganha salt novo — não reaproveita o antigo.
            var (hash, salt) = _hashingService.HashValue(req.NewPassword);

            // O autor da alteração é o próprio dono da conta, que é quem o controller identificou.
            user.ChangePassword(hash, salt, id.ToString());

            await _userRepository.UpdatePassword(user);

            // Depois de gravar, e não antes: revogar primeiro deixaria o usuário sem sessão se a
            // gravação falhasse — ele teria perdido o acesso sem ter trocado a senha.
            await RevokeActiveSessionsAsync(id);

            _logger.LogInformation("Password changed for user {UserId}", id);

            return new ChangePasswordResponse { Message = "Password updated", Success = true };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while changing password.");
            return new ChangePasswordResponse { Message = "Same error happen", Success = false };
        }
    }

    /// <summary>
    /// Revoga todos os refresh tokens ainda ativos do usuário.
    ///
    /// <para>
    /// As gravações são independentes entre si e vão juntas por <c>Task.WhenAll</c> — uma por vez
    /// num laço multiplicaria a latência do banco pelo número de sessões abertas.
    /// </para>
    /// </summary>
    private async Task RevokeActiveSessionsAsync(Guid userId)
    {
        var tokens = await _refreshTokenRepository.FindByFilter(token => token.UserId == userId);

        var revocations = tokens
            .Where(token => token.IsActive)
            .Select(token =>
            {
                token.Revoke("Password changed");
                return _refreshTokenRepository.Update(token.Id.ToString(), token);
            })
            .ToList();

        if (revocations.Count == 0) return;

        await Task.WhenAll(revocations);
        _logger.LogInformation(
            "{Count} refresh token(s) revoked after password change for user {UserId}",
            revocations.Count, userId);
    }
}
