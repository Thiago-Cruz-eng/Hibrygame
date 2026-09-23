using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.UseCases;

/// <summary>
/// Troca um refresh token válido por um access token novo — e por um refresh token novo. Caso de
/// uso de <c>POST /refresh-token</c>.
///
/// <para>
/// <b>Rotação: o token apresentado é gasto.</b> Cada refresh revoga o token usado e emite outro,
/// com <c>ReplacedByTokenId</c> apontando para o sucessor. Não é burocracia — é o que permite
/// detectar vazamento. Se um refresh token só fosse invalidado ao expirar, quem o copiasse
/// poderia usá-lo por 30 dias sem que nada indicasse o problema. Com rotação, o token roubado
/// para de funcionar no primeiro uso legítimo seguinte, e a corrente de substituições registra o
/// que aconteceu.
/// </para>
///
/// <para>
/// O cliente <b>tem de guardar o token novo</b> devolvido aqui. Continuar usando o antigo produz
/// "Invalid refresh token" a partir da segunda chamada.
/// </para>
///
/// <para>
/// <b>Reuso de token já rotacionado derruba a cadeia inteira.</b> Um refresh token revogado sendo
/// apresentado significa uma de duas coisas: ou o cliente legítimo está repetindo uma chamada, ou
/// alguém copiou o valor. Não há como distinguir, e o custo dos dois enganos é assimétrico — pedir
/// um login novo ao usuário legítimo é um incômodo; deixar o ladrão renovar por 30 dias é a conta
/// perdida. Então, ao detectar reuso, <b>todos</b> os tokens ativos daquele usuário são revogados
/// e a sessão morre nos dois lados. Era o gancho previsto em <c>ReplacedByTokenId</c>.
/// </para>
/// </summary>
public class RefreshTokenUseCase
{
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly IRefreshTokenRepositoryNoSql _refreshTokenRepository;
    private readonly ISecureHashingService _hashingService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RefreshTokenUseCase> _logger;

    public RefreshTokenUseCase(
        IUserRepositoryNoSql userRepository,
        IRefreshTokenRepositoryNoSql refreshTokenRepository,
        ISecureHashingService hashingService,
        ITokenService tokenService,
        ILogger<RefreshTokenUseCase> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _hashingService = hashingService;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// Valida o refresh token apresentado e, se ele estiver ativo, rotaciona.
    /// </summary>
    /// <returns>
    /// Access token e refresh token novos, ou <c>Success = false</c> — id inválido, usuário
    /// inexistente, ou token que não corresponde a nenhum registro ativo.
    /// </returns>
    public async Task<RefreshTokenResponse> RefreshAsync(RefreshTokenRequest req)
    {
        try
        {
            // O UserId vem como texto no corpo do request e pode ser qualquer coisa. Recusar aqui
            // evita levar lixo até a consulta.
            if (!Guid.TryParse(req.UserId, out var userId))
                return new RefreshTokenResponse { Message = "Invalid user", Success = false };

            var users = await _userRepository.FindByFilter(user => user.Id == userId);
            var user = users.FirstOrDefault();
            if (user is null)
                return new RefreshTokenResponse { Message = "User not found", Success = false };

            var refreshTokens = await _refreshTokenRepository.FindByFilter(token => token.UserId == userId);

            // Separado por estado ANTES de qualquer PBKDF2: os ativos são o caminho feliz e
            // costumam ser um só, enquanto os revogados se acumulam a cada rotação. Testar a lista
            // inteira faria o custo de um refresh crescer para sempre — um usuário que renova há
            // meses acumula centenas de registros a 100.000 iterações cada.
            var activeTokens = refreshTokens
                .Where(token => token.RevokedAt is null && token.ExpiresAt > DateTime.UtcNow)
                .ToList();

            var matchingToken = FindMatchingToken(activeTokens, req.RefreshToken);

            if (matchingToken is null)
            {
                // Não casou com nenhum ativo. Antes de recusar, vale saber se casa com um
                // REVOGADO: aí não é palpite errado, é um token que já foi gasto voltando —
                // sinal de vazamento.
                await HandleNoActiveMatchAsync(userId, req.RefreshToken, refreshTokens, activeTokens);

                // Mesma mensagem para "não existe nenhum token assim", "já foi revogado" e
                // "expirou": qualquer distinção informaria quem está tentando adivinhar tokens.
                return new RefreshTokenResponse { Message = "Invalid refresh token", Success = false };
            }

            var accessTokenResult = _tokenService.CreateAccessToken(user);
            var newRefreshTokenResult = _tokenService.CreateRefreshToken(user);

            // A ordem importa: revoga o antigo e liga-o ao sucessor ANTES de gravar o novo. Sem
            // transação no MongoDB, uma falha no meio deixa estado inconsistente — e é melhor ter
            // um token revogado sem sucessor gravado (usuário faz login de novo) do que dois
            // tokens ativos ao mesmo tempo.
            matchingToken.Revoke("Rotated", newRefreshTokenResult.Token.Id);
            await _refreshTokenRepository.Update(matchingToken.Id.ToString(), matchingToken);
            await _refreshTokenRepository.Save(newRefreshTokenResult.Token);

            return new RefreshTokenResponse
            {
                Success = true,
                AccessToken = accessTokenResult.Token,

                // O cliente tem de substituir o token que guardava por este.
                RefreshToken = newRefreshTokenResult.RawToken,

                ExpiresAt = accessTokenResult.ExpiresAt,
                Message = "Token refreshed"
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while refreshing token.");
            return new RefreshTokenResponse { Message = "Refresh failed", Success = false };
        }
    }

    /// <summary>
    /// Encontra, entre <paramref name="candidates"/>, o token que corresponde ao valor
    /// apresentado.
    ///
    /// <para>
    /// <b>Por que varrer em vez de consultar direto:</b> o banco guarda hashes com salt, e cada
    /// token tem um salt próprio. Não existe consulta possível por hash — só dá para pegar os
    /// registros do usuário e testar um a um, re-derivando o hash com o salt de cada.
    /// </para>
    ///
    /// <para>
    /// <b>É por isso que quem chama filtra antes.</b> Cada teste é um PBKDF2 de 100.000 iterações
    /// (dezenas de milissegundos), e os tokens revogados não são removidos da coleção. O caminho
    /// feliz percorre só os ativos; os revogados só são percorridos quando nenhum ativo casou, que
    /// é o caminho da detecção de reuso — e aí o custo se justifica.
    /// </para>
    /// </summary>
    private RefreshToken? FindMatchingToken(IEnumerable<RefreshToken> candidates, string rawToken) =>
        candidates.FirstOrDefault(token =>
            _hashingService.Verify(rawToken, token.TokenHash, token.Salt));

    /// <summary>
    /// Decide o que fazer quando o valor apresentado não corresponde a nenhum token ativo.
    ///
    /// <para>
    /// Se ele corresponder a um token <b>revogado</b>, é replay: o valor circulou depois de ter
    /// sido gasto. A resposta é revogar todas as sessões ativas do usuário — ver a nota da classe
    /// sobre a assimetria de custo entre os dois enganos possíveis.
    /// </para>
    /// </summary>
    private async Task HandleNoActiveMatchAsync(
        Guid userId,
        string rawToken,
        IEnumerable<RefreshToken> allTokens,
        List<RefreshToken> activeTokens)
    {
        var replayed = FindMatchingToken(allTokens.Where(token => token.RevokedAt is not null), rawToken);

        if (replayed is null)
        {
            // Nem ativo nem revogado: palpite, token de outro usuário, ou token já apagado.
            _logger.LogWarning("Invalid refresh token presented for user {UserId}", userId);
            return;
        }

        _logger.LogWarning("Refresh token reuse detected for user {UserId}", userId);

        // Revogações independentes entre si, disparadas juntas: uma por vez multiplicaria a
        // latência do banco pelo número de sessões abertas, justamente no caminho em que se quer
        // fechar tudo depressa.
        var revocations = activeTokens.Select(token =>
        {
            token.Revoke("Reuse detected");
            return _refreshTokenRepository.Update(token.Id.ToString(), token);
        }).ToList();

        if (revocations.Count == 0) return;

        await Task.WhenAll(revocations);
    }
}
