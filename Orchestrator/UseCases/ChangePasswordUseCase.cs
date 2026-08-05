using Orchestrator.Domain;
using Orchestrator.Infra.BaseRepository;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.UseCases;

/// <summary>
/// Troca a senha de um usuário que informou a senha atual corretamente.
///
/// <para>
/// <b>Limitação de segurança conhecida (DT-16):</b> este caso de uso não confere se quem está
/// pedindo é o dono da conta — ele confia no <c>UserId</c> que vem no corpo do request. A
/// exigência da senha atual limita o dano, mas a checagem de identidade continua faltando. Ver
/// <c>docs/debito-tecnico.md</c> antes de mexer aqui.
/// </para>
/// </summary>
public class ChangePasswordUseCase
{
    /// <summary>
    /// Este é o único caso de uso que depende do CRUD genérico em vez de um
    /// <c>I{Entidade}RepositoryNoSql</c>, e não é descuido: ele precisa de
    /// <see cref="IGenericRepository.Update{T}"/>, que atualiza <b>campos escolhidos</b>. O
    /// contrato estreito só oferece substituição do documento inteiro, e trocar senha
    /// reescrevendo o usuário todo abriria espaço para desfazer, sem querer, qualquer campo
    /// que tivesse mudado no meio.
    /// </summary>
    private readonly IGenericRepository _genericRepository;
    private readonly ISecureHashingService _hashingService;
    private readonly ILogger<ChangePasswordUseCase> _logger;

    public ChangePasswordUseCase(
        IGenericRepository genericRepository,
        ISecureHashingService hashingService,
        ILogger<ChangePasswordUseCase> logger)
    {
        _genericRepository = genericRepository;
        _hashingService = hashingService;
        _logger = logger;
    }

    /// <summary>
    /// Confere a senha atual e, se ela bater, grava o hash da nova.
    ///
    /// <para>
    /// Como todo caso de uso do projeto: nunca lança exceção para o controller. Falha vira
    /// <c>Success = false</c> com mensagem, e o controller traduz isso em status HTTP.
    /// </para>
    /// </summary>
    public async Task<ChangePasswordResponse> ChangeAsync(ChangePasswordRequest req)
    {
        try
        {
            var user = await _genericRepository.GetFirstOrDefault<User>(user => user.Id.ToString() == req.UserId);
            if (user is null)
                return new ChangePasswordResponse { Message = "User not found", Success = false };

            // Senha nunca é comparada em claro: `Verify` re-deriva o hash da senha informada
            // usando o salt guardado e compara os hashes.
            if (!_hashingService.Verify(req.CurrentPassword, user.PasswordHash, user.Salt))
                return new ChangePasswordResponse { Message = "Invalid credentials", Success = false };

            // Senha nova ganha salt novo — não reaproveita o antigo.

            var (hash, salt) = _hashingService.HashValue(req.NewPassword);
            user.ChangePassword(hash, salt, req.ModifiedBy.Trim());

            // ChangePassword preenche ModificationInformations. Materializar aqui em vez
            // de gravar o campo possivelmente nulo direto: se algum dia deixar de
            // preencher, falha com mensagem clara em vez de escrever null na auditoria.
            var modification = user.ModificationInformations
                ?? throw new InvalidOperationException(
                    "ChangePassword deveria ter preenchido ModificationInformations.");

            await _genericRepository.Update<User>(us => us.Id == user.Id,
                CancellationToken.None,
                (x => x.PasswordHash, user.PasswordHash),
                (x => x.Salt, user.Salt),
                (x => x.MustChangePassword, user.MustChangePassword),
                // `!` no seletor: a propriedade e anulavel no dominio, mas aqui ela e
                // apenas o nome do campo a atualizar, nunca um valor lido.
                (x => x.ModificationInformations!, modification)
                );

            return new ChangePasswordResponse { Message = "Password updated", Success = true };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while changing password.");
            return new ChangePasswordResponse { Message = "Same error happen", Success = false };
        }
    }
}
