using Orchestrator.Domain;

namespace Orchestrator.UseCases.Interfaces;

/// <summary>
/// Autorização de sessão de jogo: registra e consulta a permissão de um usuário para mover uma cor
/// numa sala.
///
/// <para>
/// <b>Leia antes de mexer.</b> Este subdomínio é uma segunda camada de autorização, paralela à do
/// hub — e o hub já revalida por conta própria identidade, turno, posse da peça, legalidade do
/// lance e auto-xeque, sem consultar nada disto. Existe uma decisão humana pendente sobre se esta
/// camada deve continuar existindo. Ver <c>docs/debito-tecnico.md</c> e a skill
/// <c>validacao-de-sessao-de-jogo</c>.
/// </para>
///
/// <para>
/// Consequência prática: <b>não construa nada novo em cima desta interface</b> e não amplie o que
/// ela autoriza. Corrigir o que já está aqui, sim.
/// </para>
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Registra a autorização, no login. Sala e cor entram vazias — só passam a ser conhecidas
    /// quando o jogador escolhe uma sala.
    /// </summary>
    /// <returns>
    /// <c>false</c> em caso de falha, já registrada em log. <b>O login não é interrompido</b> por
    /// isto: <c>LoginAsyncUseCase</c> ignora o retorno.
    /// </returns>
    Task<bool> CreateValidation(ValidationDto req);

    /// <summary>
    /// A autorização de um usuário para um access token.
    ///
    /// <para>
    /// <b>Único membro desta interface que lança exceção</b> em vez de devolver ausência —
    /// <see cref="InvalidOperationException"/> quando não existe registro. Os outros seguem o
    /// padrão do projeto de nunca lançar. Quem chamar precisa tratar.
    /// </para>
    /// </summary>
    Task<Validation> GetValidationByUserToken(string userId, string accessToken);

    /// <summary>
    /// Grava a sala e a cor na autorização existente, quando o jogador entra numa sala.
    /// </summary>
    /// <returns><c>false</c> se não houver autorização para aquele usuário e token.</returns>
    Task<bool> UpdateValidationByUserToken(string userId, string accessToken, string pieceColor, string room);

    /// <summary>
    /// O usuário está autorizado a mover a cor <paramref name="colorPiece"/> na sala
    /// <paramref name="room"/>?
    ///
    /// <para>
    /// <b>Não é o que decide se um lance acontece.</b> Quem decide é <c>ChessHub.MakeMove</c>, que
    /// revalida tudo sozinho e não chama este método.
    /// </para>
    /// </summary>
    Task<bool> GetValidationCanMove(string userId, string token, string colorPiece, string room);

    // Removidos daqui:
    //   GetValidationByUserIdTokenAndRoom  era apenas `throw new NotImplementedException()`
    //                                      e nao tinha um unico chamador
    //   os parametros `email` e `day`       de GetValidationCanMove: eram recebidos e
    //                                      descartados, com os filtros comentados no
    //                                      corpo. Ver DT-05 e DT-20.
}
