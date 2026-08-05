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

            var matchingToken = await FindMatchingToken(userId, req.RefreshToken);

            // Mesma mensagem para "não existe nenhum token assim" e "existe mas já foi revogado ou
            // expirou": qualquer distinção informaria quem está tentando adivinhar tokens.
            if (matchingToken is null || !matchingToken.IsActive)
                return new RefreshTokenResponse { Message = "Invalid refresh token", Success = false };

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
    /// Encontra, entre os refresh tokens do usuário, aquele que corresponde ao valor apresentado.
    ///
    /// <para>
    /// <b>Por que varrer em vez de consultar direto:</b> o banco guarda hashes com salt, e cada
    /// token tem um salt próprio. Não existe consulta possível por hash — só dá para pegar os
    /// registros do usuário e testar um a um, re-derivando o hash com o salt de cada.
    /// </para>
    ///
    /// <para>
    /// <b>Consequência de custo, e ela é real:</b> cada teste é um PBKDF2 de 100.000 iterações
    /// (dezenas de milissegundos). Como os tokens revogados <b>não</b> são removidos da coleção,
    /// a lista cresce a cada refresh: um usuário que renova há meses acumula centenas de registros,
    /// e um refresh dele passa a custar segundos. Duas saídas, se isso incomodar: limpar tokens
    /// revogados e expirados periodicamente, ou filtrar a consulta por <c>IsActive</c> antes de
    /// testar. Nenhuma das duas existe hoje.
    /// </para>
    /// </summary>
    private async Task<RefreshToken?> FindMatchingToken(Guid userId, string rawToken)
    {
        var refreshTokens = await _refreshTokenRepository.FindByFilter(token => token.UserId == userId);

        return refreshTokens.FirstOrDefault(token =>
            _hashingService.Verify(rawToken, token.TokenHash, token.Salt));
    }
}
