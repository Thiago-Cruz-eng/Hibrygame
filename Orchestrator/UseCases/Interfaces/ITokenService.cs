using Orchestrator.Domain;

namespace Orchestrator.UseCases.Interfaces;

/// <summary>
/// Emissão das duas credenciais que o login devolve.
///
/// <para>
/// <b>Por que são dois tokens</b> — cada um resolve um problema que o outro não resolve:
/// </para>
/// <list type="bullet">
///   <item><description>
///   <b>access token</b> — é o que acompanha cada requisição. Curto (60 min por padrão) porque
///   não é revogável: como ele se valida apenas pela assinatura, sem ida ao banco, não há onde
///   marcar "este não vale mais";
///   </description></item>
///   <item><description>
///   <b>refresh token</b> — serve só para obter um access token novo. Longo (30 dias), e pode ser
///   revogado, porque existe como registro no banco.
///   </description></item>
/// </list>
///
/// <para>
/// A combinação dá o melhor dos dois: validação de requisição barata e sem consulta ao banco, e
/// ainda assim a capacidade de cortar o acesso de alguém.
/// </para>
///
/// <para>
/// Este serviço apenas <b>emite</b>. Ele não valida token (isso é do middleware configurado no
/// <c>Program.cs</c>), não grava nada no banco e não revoga — quem persiste e revoga é
/// <c>RefreshTokenUseCase</c>.
/// </para>
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Emite o access token de <paramref name="user"/>, com os claims de identidade e papel.
    /// </summary>
    AccessTokenResult CreateAccessToken(User user);

    /// <summary>
    /// Sorteia um refresh token para <paramref name="user"/> e monta a entidade correspondente.
    ///
    /// <para>
    /// <b>Não salva no banco.</b> Devolve a entidade pronta e quem chamou é que persiste. O motivo
    /// é o valor em claro: ele existe apenas no retorno desta chamada, e a separação deixa
    /// explícito que gravar e entregar são passos distintos.
    /// </para>
    /// </summary>
    RefreshTokenIssueResult CreateRefreshToken(User user);
}

/// <summary>
/// Um access token recém-emitido.
/// </summary>
/// <param name="Token">O JWT assinado, pronto para ir no cabeçalho <c>Authorization</c>.</param>
/// <param name="ExpiresAt">
/// Quando expira, em UTC. Devolvido junto para que o cliente possa renovar <b>antes</b> de tomar
/// 401, em vez de descobrir a expiração por uma requisição que falhou.
/// </param>
public record AccessTokenResult(string Token, DateTime ExpiresAt);

/// <summary>
/// Um refresh token recém-emitido, nas suas duas formas.
///
/// <para>
/// A separação entre <paramref name="RawToken"/> e <paramref name="Token"/> é o ponto todo deste
/// tipo, e vale entender: o valor em claro vai para o cliente e nunca é gravado; a entidade tem
/// apenas o hash e é o que vai para o banco. Depois desta chamada o valor em claro é
/// irrecuperável.
/// </para>
/// </summary>
/// <param name="RawToken">
/// O valor em claro, para entregar ao cliente. <b>Nunca</b> registre em log nem grave.
/// </param>
/// <param name="Token">
/// A entidade a persistir — contém o hash e o salt, não o valor. Ainda <b>não</b> foi salva.
/// </param>
public record RefreshTokenIssueResult(string RawToken, RefreshToken Token);
