using System.ComponentModel.DataAnnotations;

namespace Orchestrator.UseCases.Dto.Request;

// Os quatro requests de /validation vivem no mesmo arquivo porque são variações do mesmo par de
// campos e só fazem sentido lidos em conjunto.
//
// Duas coisas valem para todos eles:
//
//   1. Todos os endpoints de /validation são POST, mesmo os que só leem. É deliberado: eles
//      recebem UserId e são autorizados por token, e token nunca vai em URL — URL aparece em log
//      de servidor, em histórico de navegador e no cabeçalho Referer.
//
//   2. Todos carregam UserId no corpo, e o controller compara esse valor com o claim `sub` do
//      token, respondendo 403 se divergirem. É defesa em profundidade: o UserId do corpo nunca é
//      confiado por si só.
//
// Ver a nota em IValidationService sobre a decisão pendente quanto a este subdomínio.

/// <summary>
/// Corpo de <c>POST /validation/verify</c>: existe autorização de sessão para este usuário?
/// </summary>
public class VerifyValidationRequest
{
    /// <summary>
    /// Usuário a verificar. Precisa coincidir com o <c>sub</c> do token, ou a resposta é 403.
    /// </summary>
    [Required]
    public string UserId { get; set; } = null!;
}

/// <summary>
/// Corpo de <c>POST /validation/get</c>: devolve a autorização de sessão do usuário.
///
/// <para>
/// Idêntico a <see cref="VerifyValidationRequest"/> em forma. São dois tipos e não um porque
/// representam operações diferentes — assim uma delas pode ganhar campo novo sem arrastar a outra.
/// </para>
/// </summary>
public class GetValidationRequest
{
    /// <summary>Usuário consultado. Conferido contra o <c>sub</c> do token.</summary>
    [Required]
    public string UserId { get; set; } = null!;
}

/// <summary>
/// Corpo de <c>POST /validation/update</c>: grava sala e cor na autorização, quando o jogador entra
/// numa sala.
/// </summary>
public class UpdateValidationRequest
{
    /// <summary>Usuário. Conferido contra o <c>sub</c> do token.</summary>
    [Required]
    public string UserId { get; set; } = null!;

    /// <summary>Sala em que o jogador entrou.</summary>
    [Required]
    public string Room { get; set; } = null!;

    /// <summary>Cor atribuída: <c>"White"</c> ou <c>"Black"</c>. O valor não é validado.</summary>
    [Required]
    public string PieceColor { get; set; } = null!;

    /// <summary>
    /// E-mail do usuário. <b>Obrigatório no request e não usado na atualização</b> — o serviço
    /// localiza o registro por usuário e token, e grava apenas sala e cor. Ver DT-20.
    /// </summary>
    [Required, EmailAddress]
    public string UserEmail { get; set; } = null!;
}

/// <summary>
/// Corpo de <c>POST /validation/can-move</c>: o usuário pode mover esta cor nesta sala?
///
/// <para>
/// <b>Isto não decide lance nenhum.</b> Quem autoriza um lance é <c>ChessHub.MakeMove</c>, que
/// revalida turno, posse e legalidade por conta própria e não consulta este endpoint. Serve ao
/// frontend como checagem de tela.
/// </para>
/// </summary>
public class CanMoveValidationRequest
{
    /// <summary>Usuário. Conferido contra o <c>sub</c> do token.</summary>
    [Required]
    public string UserId { get; set; } = null!;

    /// <summary>Sala. Entra no filtro.</summary>
    [Required]
    public string Room { get; set; } = null!;

    /// <summary>Cor a mover. Entra no filtro.</summary>
    [Required]
    public string PieceColor { get; set; } = null!;

    /// <summary>
    /// E-mail do usuário. <b>Obrigatório e ignorado</b>: o filtro de
    /// <c>GetValidationCanMove</c> usa token, usuário, sala e cor — não o e-mail. Ver DT-20.
    /// </summary>
    [Required, EmailAddress]
    public string UserEmail { get; set; } = null!;

    /// <summary>
    /// Dia da partida. <b>Obrigatório e ignorado</b>, como <see cref="UserEmail"/>. A autorização
    /// não expira por data, então enviar qualquer valor dá no mesmo. Ver DT-20.
    ///
    /// <para>
    /// É <c>string</c> e não <see cref="DateTime"/>, o que reforça que nada é feito com ele — nem
    /// parsing.
    /// </para>
    /// </summary>
    [Required]
    public string Day { get; set; } = null!;
}
