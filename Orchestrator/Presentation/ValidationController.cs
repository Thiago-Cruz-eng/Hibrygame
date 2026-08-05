using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.Presentation;

/// <summary>
/// Endpoints de autorização de sessão de jogo.
///
/// <para>
/// <b>Todos são POST, inclusive os que só leem.</b> Não é descuido: eles são autorizados pelo
/// access token e recebem o <c>UserId</c> no corpo. Token nunca vai em URL — URL aparece em log de
/// servidor, em histórico de navegador e no cabeçalho <c>Referer</c>, e um token que vaza por lá
/// vale até expirar. A única exceção no projeto é o <c>?access_token=</c> do <c>/chesshub</c>, que
/// existe porque WebSocket não carrega cabeçalho próprio.
/// </para>
///
/// <para>
/// <b>Defesa em profundidade:</b> a policy garante que o chamador está autenticado como jogador;
/// <see cref="IsCallerAuthorizedFor"/> garante que ele está operando sobre a <b>própria</b> conta,
/// comparando o claim <c>sub</c> com o <c>UserId</c> do corpo. Sem essa segunda checagem, qualquer
/// jogador leria a validação de qualquer outro. <b>Toda action nova deste controller precisa
/// chamá-la antes de qualquer outra coisa.</b>
/// </para>
///
/// <para>
/// Ver a nota em <see cref="IValidationService"/> sobre a decisão pendente quanto a este
/// subdomínio.
/// </para>
/// </summary>
[ApiController]
[Authorize(Policy = "Role:Player")]
[Route("validation")]
public class ValidationController : ControllerBase
{
    private readonly IValidationService _validationService;

    public ValidationController(IValidationService validationService)
    {
        _validationService = validationService;
    }

    /// <summary>
    /// <c>POST /validation/verify</c> — existe autorização de sessão para este usuário?
    /// </summary>
    /// <remarks>
    /// <b>ATENÇÃO — defeito conhecido, não corrigido aqui.</b> O <c>validation is not null</c>
    /// abaixo é inalcançável em produção: <see cref="IValidationService.GetValidationByUserToken"/>
    /// <b>lança</b> <see cref="InvalidOperationException"/> quando não encontra registro, em vez de
    /// devolver <c>null</c>. Logo, o caso "não existe autorização" não devolve
    /// <c>Valid = false</c> — devolve <b>500</b>.
    ///
    /// <para>
    /// A suíte não pega isso porque o teste
    /// <c>Verify_HappyPath_ValidationNull_ReturnsOkWithValidFalse</c> configura o mock para
    /// devolver <c>null</c>, algo que a implementação real nunca faz. O teste passa contra um
    /// comportamento que não existe.
    /// </para>
    ///
    /// <para>
    /// Corrigir exige escolher uma das duas convenções e alinhar serviço, controller e testes:
    /// o serviço passa a devolver <c>Validation?</c> (coerente com o resto do projeto, que nunca
    /// lança), ou o controller captura a exceção. É decisão de contrato — não mude só um dos três.
    /// </para>
    /// </remarks>
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyValidationRequest req)
    {
        if (!IsCallerAuthorizedFor(req.UserId, out var token))
            return Forbid();

        var validation = await _validationService.GetValidationByUserToken(req.UserId, token);
        return Ok(new VerifyValidationResponse { Valid = validation is not null });
    }

    /// <summary>
    /// <c>POST /validation/get</c> — devolve a autorização de sessão do usuário.
    /// </summary>
    /// <remarks>
    /// Mesmo defeito descrito em <see cref="Verify"/>: o <c>NotFound</c> abaixo é inalcançável em
    /// produção, porque o serviço lança em vez de devolver <c>null</c>. Na ausência de registro este
    /// endpoint responde 500, não 404.
    /// </remarks>
    [HttpPost("get")]
    public async Task<IActionResult> Get([FromBody] GetValidationRequest req)
    {
        if (!IsCallerAuthorizedFor(req.UserId, out var token))
            return Forbid();

        var validation = await _validationService.GetValidationByUserToken(req.UserId, token);
        if (validation is null)
            return NotFound();

        // Montagem campo a campo, e não a entidade inteira: é isto que mantém o access token
        // (gravado em claro, DT-07) fora da resposta.
        return Ok(new GetValidationResponse
        {
            Id = validation.Id.ToString(),
            UserId = validation.UserId,
            UserEmail = validation.UserEmail,
            Room = validation.Room,
            PieceColor = validation.PieceColor,
            DayOfGame = validation.DayOfGame
        });
    }

    /// <summary>
    /// <c>POST /validation/update/{id}</c> — grava sala e cor na autorização.
    /// </summary>
    /// <param name="id">
    /// <b>Ignorado.</b> A rota declara o parâmetro, mas o registro é localizado por usuário e token,
    /// não por este id. Está na assinatura porque está na rota; enviar qualquer valor dá no mesmo.
    /// </param>
    /// <remarks>
    /// Responde 200 mesmo quando nada foi atualizado — o resultado está em
    /// <c>UpdateValidationResponse.Updated</c>, não no status HTTP.
    /// </remarks>
    [HttpPost("update/{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateValidationRequest req)
    {
        if (!IsCallerAuthorizedFor(req.UserId, out var token))
            return Forbid();

        // req.UserEmail nao entra na chamada: o servico localiza por usuario e token. Ver DT-20.
        var updated = await _validationService.UpdateValidationByUserToken(
            req.UserId, token, req.PieceColor, req.Room);

        return Ok(new UpdateValidationResponse { Updated = updated });
    }

    /// <summary>
    /// <c>POST /validation/can-move</c> — o usuário está autorizado a mover esta cor nesta sala?
    ///
    /// <para>
    /// <b>Não é o que decide um lance.</b> <c>ChessHub.MakeMove</c> revalida turno, posse e
    /// legalidade por conta própria e não consulta este endpoint. Serve ao frontend como checagem
    /// de tela.
    /// </para>
    /// </summary>
    [HttpPost("can-move")]
    public async Task<IActionResult> CanMove([FromBody] CanMoveValidationRequest req)
    {
        if (!IsCallerAuthorizedFor(req.UserId, out var token))
            return Forbid();

        // req.UserEmail e req.Day continuam no corpo do request (contrato de FE), mas
        // nunca chegavam ao filtro — os dois eram descartados dentro do servico. Ver DT-20.
        var canMove = await _validationService.GetValidationCanMove(
            req.UserId, token, req.PieceColor, req.Room);

        return Ok(new CanMoveValidationResponse { CanMove = canMove });
    }

    /// <summary>
    /// Confere que quem chama é o dono da conta que o request menciona e recupera o access token
    /// bruto da requisição.
    ///
    /// <para>
    /// Faz duas coisas de uma vez — validar e extrair — porque as duas dependem do mesmo contexto
    /// HTTP e nenhuma action precisa de uma sem a outra.
    /// </para>
    /// </summary>
    /// <param name="requestUserId">O <c>UserId</c> que veio no corpo do request.</param>
    /// <param name="token">
    /// O access token bruto, como o cliente o enviou. É ele que o serviço usa para localizar o
    /// registro de validação — que guarda o token <b>em claro</b> (DT-07).
    /// </param>
    /// <returns>
    /// <c>false</c> em três situações, todas tratadas como 403: o token não traz <c>sub</c>, o
    /// <c>sub</c> difere do <paramref name="requestUserId"/>, ou o token bruto não está disponível.
    /// </returns>
    private bool IsCallerAuthorizedFor(string requestUserId, out string token)
    {
        token = string.Empty;

        // Encontrado por este nome porque Program.cs desliga o mapeamento de claims. Com o
        // mapeamento ligado, `sub` viraria ClaimTypes.NameIdentifier e isto devolveria null — o que
        // fazia os QUATRO endpoints deste controller responderem 403 para todo mundo, sempre.
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        // Comparação Ordinal: identidade é sequência de bytes, não texto de idioma nenhum.
        // Comparação sensível à cultura poderia considerar iguais dois ids diferentes.
        if (string.IsNullOrEmpty(sub) || !string.Equals(sub, requestUserId, StringComparison.Ordinal))
            return false;

        // O token bruto só está disponível porque Program.cs liga SaveToken = true.
        //
        // GetAwaiter().GetResult() é chamada bloqueante dentro de um método síncrono. Funciona
        // porque o token já está em memória — não há E/S por trás, o Task volta completo. Ainda
        // assim é padrão a evitar: se este helper virar async, prefira `await`.
        var raw = HttpContext.GetTokenAsync("access_token").GetAwaiter().GetResult();
        if (string.IsNullOrEmpty(raw))
            return false;

        token = raw;
        return true;
    }
}
