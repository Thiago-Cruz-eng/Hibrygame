using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Orchestrator.Composition;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Security.Authorization;

namespace Orchestrator.Presentation;

/// <summary>
/// Endpoints de usuário e de sessão.
///
/// <para>
/// <b>O que um controller faz neste projeto, e só isso:</b> recebe o request, chama <b>um</b> caso
/// de uso e traduz o resultado em status HTTP. Ele não consulta banco, não aplica regra de negócio
/// e não trata exceção — os casos de uso já garantem que nenhuma sobe até aqui.
/// </para>
///
/// <para>
/// <b>A exceção são as checagens de autorização</b> em <see cref="CreateUser"/>,
/// <see cref="GetUser"/>, <see cref="UpdateUser"/>, <see cref="DeleteUser"/> e
/// <see cref="ChangePassword"/>. Elas estão aqui, e não no caso de uso, porque dependem de quem
/// está chamando — informação que vive no token HTTP e que o caso de uso, por não conhecer HTTP,
/// não tem como obter. O que o controller faz é <b>extrair</b> a identidade e o nível do token; a
/// regra que depende de ler o banco (o papel atual do alvo) mora no caso de uso, que recebe o
/// nível por parâmetro.
/// </para>
///
/// <para>
/// <b>Os quatro endpoints que lidam com credencial</b> — <c>/login</c>, <c>/register</c>,
/// <c>/refresh-token</c> e <c>/users/change-password</c> — carregam
/// <c>[EnableRateLimiting]</c> com a política estreita. Endpoint de credencial novo precisa
/// declarar o atributo: nada o aplica por convenção, e esquecer não quebra nada visivelmente —
/// apenas deixa a porta sem teto. Ver <see cref="RateLimitingComposition"/>.
/// </para>
///
/// <para>
/// <b>Atenção à rota.</b> O <c>[Route("api/v1/[controller]")]</c> abaixo está <b>sem efeito</b>:
/// toda action declara rota absoluta (começando com <c>/</c>), e rota absoluta ignora o prefixo da
/// classe. Os endpoints reais são <c>/login</c>, <c>/register</c>, <c>/users</c> e afins — <b>não</b>
/// <c>/api/v1/User/...</c>. O atributo está mantido para não mexer no contrato com o frontend; se
/// um dia o versionamento passar a valer, as rotas absolutas das actions é que precisam mudar.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class UserController : ControllerBase
{
    private readonly LoginAsyncUseCase _loginAsync;
    private readonly CreateUserUseCase _createUserUseCase;
    private readonly GetUserUseCase _getUserUseCase;
    private readonly UpdateUserUseCase _updateUserUseCase;
    private readonly DeleteUserUseCase _deleteUserUseCase;
    private readonly ChangePasswordUseCase _changePasswordUseCase;
    private readonly RefreshTokenUseCase _refreshTokenUseCase;
    private readonly RegisterUserUseCase _registerUserUseCase;

    // Oito casos de uso injetados — um por ação. É bastante, e é o preço de "um caso de uso por
    // ação" sem mediator: cada dependência aqui é explícita e rastreável. Se este número crescer
    // muito mais, o sinal é de que o controller está juntando responsabilidades demais e deveria
    // ser dividido por assunto (sessão x cadastro), não de que falta um mediator.
    public UserController(
        CreateUserUseCase createUserUseCase,
        GetUserUseCase getUserUseCase,
        LoginAsyncUseCase loginAsync,
        UpdateUserUseCase updateUserUseCase,
        DeleteUserUseCase deleteUserUseCase,
        ChangePasswordUseCase changePasswordUseCase,
        RefreshTokenUseCase refreshTokenUseCase,
        RegisterUserUseCase registerUserUseCase)
    {
        _createUserUseCase = createUserUseCase;
        _getUserUseCase = getUserUseCase;
        _loginAsync = loginAsync;
        _updateUserUseCase = updateUserUseCase;
        _deleteUserUseCase = deleteUserUseCase;
        _changePasswordUseCase = changePasswordUseCase;
        _refreshTokenUseCase = refreshTokenUseCase;
        _registerUserUseCase = registerUserUseCase;
    }

    /// <summary>
    /// <c>POST /login</c> — autentica e devolve a sessão.
    ///
    /// <para>
    /// Anônimo por necessidade: é este endpoint que produz a credencial, então não pode exigir uma.
    /// </para>
    /// </summary>
    /// <returns>200 com a sessão, ou <b>401</b> quando as credenciais não conferem.</returns>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingComposition.AuthPolicyName)]
    [HttpPost("/login")]
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(LoginResponse))]
    public async Task<IActionResult> LoginUser(LoginRequest req)
    {
        var result = await _loginAsync.LoginAsync(req);
        if (!result.Success)
        {
            // 401 e não 400: o request estava bem formado, a credencial é que não serve.
            return Unauthorized(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// <c>POST /refresh-token</c> — troca um refresh token válido por credenciais novas.
    ///
    /// <para>
    /// Anônimo <b>de propósito</b>, e não por descuido: o access token já expirou quando este
    /// endpoint é chamado — é exatamente esse o motivo da chamada. Quem autentica aqui é o próprio
    /// refresh token, conferido no caso de uso.
    /// </para>
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingComposition.AuthPolicyName)]
    [HttpPost("/refresh-token")]
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(RefreshTokenResponse))]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest req)
    {
        var result = await _refreshTokenUseCase.RefreshAsync(req);
        if (!result.Success)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// <c>POST /register</c> — auto-registro. Papel e autor sao decididos no servidor: o corpo do
    /// request nao tem como pedir "super adm". Devolve sessao pronta, para o cliente nao precisar
    /// fazer login logo depois.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingComposition.AuthPolicyName)]
    [HttpPost("/register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var result = await _registerUserUseCase.RegisterAsync(req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// <c>POST /users</c> — criacao administrativa: aqui o papel PODE vir pelo corpo, e por isso o
    /// endpoint exige Role:Admin.
    ///
    /// Era [AllowAnonymous], o que permitia a qualquer pessoa na internet criar um
    /// "super adm" e depois apagar usuarios (DT-04). Quem quer apenas uma conta de
    /// jogador usa POST /register.
    ///
    /// Duas travas alem da politica: CreatedBy vem do claim `sub`, nunca do corpo, e
    /// ninguem cria papel acima do proprio nivel.
    ///
    /// <para>
    /// <b>É o único método deste controller com lógica própria</b>, e as quatro checagens abaixo
    /// estão em ordem deliberada: identidade do chamador, nível do chamador, validade do papel
    /// pedido e comparação entre os dois. Ao mexer, mantenha a ordem — trocar as duas últimas faria
    /// papel inválido ser reportado como "acima do seu nível".
    /// </para>
    /// </summary>
    [Authorize(Policy = "Role:Admin")]
    [HttpPost("/users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest req)
    {
        // O claim `sub` só é encontrado por este nome porque Program.cs desliga o mapeamento de
        // claims (MapInboundClaims = false). Com o mapeamento ligado, isto devolveria null e todo
        // request cairia no Forbid abaixo.
        var callerId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(callerId)) return Forbid();

        if (!TryGetCallerRoleLevel(out var callerLevel)) return Forbid();

        if (!RoleHierarchy.TryGetLevel(req.Role ?? string.Empty, out var requestedLevel))
            return BadRequest(new CreateUserResponse { Success = false, Message = "Invalid role" });

        // Impede escalada de privilégio: um "adm" não cria um "super adm". A policy do endpoint
        // garante apenas que o chamador é ao menos Admin; ela não sabe qual papel ele está pedindo.
        if (requestedLevel > callerLevel)
        {
            return BadRequest(new CreateUserResponse
            {
                Success = false,
                Message = "Cannot create a user with a role above your own."
            });
        }

        // Auditoria vem do token, nao do corpo: CreatedBy era forjavel.
        req.CreatedBy = callerId;

        var result = await _createUserUseCase.CreateAsync(req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// <c>GET /users/{id}</c> — devolve um usuário.
    ///
    /// <para>
    /// <b>Só a própria conta, ou quem for ao menos <c>adm</c>.</b> Antes qualquer jogador
    /// autenticado lia qualquer usuário: com os ids sendo Guid, isso não era uma listagem, mas
    /// bastava conhecer um id para obter nome, e-mail, papel e vínculos de outra pessoa. A
    /// resposta nunca incluiu hash de senha nem salt (ver <see cref="GetUserResponse"/>).
    /// </para>
    ///
    /// <para>
    /// <b>Recusa como 404, e não 403</b>, de propósito: 403 confirmaria que aquele id existe, e
    /// confirmar existência é metade do trabalho de quem está sondando. Para quem não tem
    /// permissão, "não existe" e "não é seu" são a mesma resposta.
    /// </para>
    /// </summary>
    /// <returns>200 com o usuário, ou 404 — que cobre não existir, não ser seu, e falha na consulta.</returns>
    [Authorize(Policy = "Role:Player")]
    [HttpGet("/users/{id}")]
    public async Task<IActionResult> GetUser(string id)
    {
        var callerId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(callerId)) return NotFound();

        var isSelf = string.Equals(callerId, id, StringComparison.Ordinal);
        var isAdministrator = TryGetCallerRoleLevel(out var callerLevel) && callerLevel >= RoleLevel.Admin;

        if (!isSelf && !isAdministrator) return NotFound();

        var result = await _getUserUseCase.GetAsync(id);
        if (result is null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    /// <summary>
    /// <c>PUT /users/{id}</c> — substitui os dados do usuário.
    ///
    /// <para>
    /// <b>Limitado por alçada nas duas pontas</b> (era a DT-16): o caso de uso recebe o nível do
    /// chamador e recusa tanto alterar quem está acima dele quanto conceder papel acima do dele.
    /// A checagem não cabe inteira aqui porque uma das metades depende de ler o usuário alvo no
    /// banco, e controller não consulta banco.
    /// </para>
    ///
    /// <para>
    /// <b><c>ModifiedBy</c> do corpo é ignorado</b>: a auditoria grava o claim <c>sub</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Status HTTP impreciso, e é herdado:</b> qualquer falha vira <b>404</b>, inclusive
    /// "Email already in use" e "Invalid role", que são 400 por natureza. A mensagem no corpo
    /// distingue os casos; o código de status não. Corrigir é mudança de contrato — registre em
    /// <c>docs/FRONTEND_CHANGES.md</c> se for feito.
    /// </para>
    /// </summary>
    [Authorize(Policy = "Role:TeamLeader")]
    [HttpPut("/users/{id}")]
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(UpdateUserResponse))]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest req)
    {
        var callerId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(callerId)) return Forbid();

        if (!TryGetCallerRoleLevel(out var callerLevel)) return Forbid();

        var result = await _updateUserUseCase.UpdateAsync(id, req, callerLevel, callerId);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// <c>DELETE /users/{id}</c> — remove o usuário, em definitivo.
    ///
    /// <para>
    /// Remoção física, sem exclusão lógica e sem como desfazer. Os refresh tokens do usuário são
    /// revogados e os registros de validação removidos junto — ver <c>DeleteUserUseCase</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Ninguém apaga quem está acima de si:</b> o nível do chamador vai para o caso de uso, que
    /// compara com o papel atual do alvo. A policy sozinha não faz isso — ela garante apenas que
    /// o chamador é ao menos <c>adm</c>, sem saber quem ele quer apagar.
    /// </para>
    /// </summary>
    [Authorize(Policy = "Role:Admin")]
    [HttpDelete("/users/{id}")]
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DeleteUserResponse))]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var callerId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(callerId)) return Forbid();

        if (!TryGetCallerRoleLevel(out var callerLevel)) return Forbid();

        var result = await _deleteUserUseCase.DeleteAsync(id, callerLevel, callerId);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// <c>POST /users/change-password</c> — troca a <b>própria</b> senha, exigindo a atual.
    ///
    /// <para>
    /// <b>O alvo é sempre o claim <c>sub</c>.</b> <c>ChangePasswordRequest.UserId</c> e
    /// <c>ModifiedBy</c> são ignorados — continuam no DTO só para não quebrar o cliente atual.
    /// Era a DT-16: qualquer jogador autenticado trocava a senha de qualquer usuário, bastando
    /// saber a senha atual dele.
    /// </para>
    ///
    /// <para>
    /// Trocar a senha revoga os refresh tokens do usuário, então as outras sessões morrem — ver
    /// <c>ChangePasswordUseCase</c>.
    /// </para>
    /// </summary>
    /// <returns>200, ou <b>401</b> — a falha esperada aqui é senha atual errada.</returns>
    [Authorize(Policy = "Role:Player")]
    [EnableRateLimiting(RateLimitingComposition.AuthPolicyName)]
    [HttpPost("/users/change-password")]
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(ChangePasswordResponse))]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var callerId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(callerId)) return Forbid();

        var result = await _changePasswordUseCase.ChangeAsync(callerId, req);
        if (!result.Success)
        {
            return Unauthorized(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// O nível hierárquico de quem está chamando, lido do claim de papel do token.
    /// </summary>
    /// <returns>
    /// <c>false</c> quando o token não traz papel, ou traz um papel que <c>RoleHierarchy</c> não
    /// reconhece. Quem chama trata isso como proibido — não como erro.
    /// </returns>
    private bool TryGetCallerRoleLevel(out RoleLevel level)
    {
        level = default;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        return !string.IsNullOrEmpty(role) && RoleHierarchy.TryGetLevel(role, out level);
    }
}
