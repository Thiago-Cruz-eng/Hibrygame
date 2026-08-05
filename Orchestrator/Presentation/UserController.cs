using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
/// <b>A exceção é <see cref="CreateUser"/></b>, que faz checagens de autorização antes de delegar.
/// Elas estão aqui, e não no caso de uso, porque dependem de quem está chamando — informação que
/// vive no token HTTP e que o caso de uso, por não conhecer HTTP, não tem como obter.
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
    /// <b>Qualquer jogador autenticado lê qualquer usuário</b>, não só o próprio. A resposta não
    /// inclui hash de senha nem salt (ver <see cref="GetUserResponse"/>), então o que se expõe é
    /// nome, e-mail, papel e vínculos.
    /// </para>
    /// </summary>
    /// <returns>200 com o usuário, ou 404 — que cobre tanto "não existe" quanto falha na consulta.</returns>
    [Authorize(Policy = "Role:Player")]
    [HttpGet("/users/{id}")]
    public async Task<IActionResult> GetUser(string id)
    {
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
    /// <b>Não confere se quem chama é o dono da conta</b> (DT-16): um <c>lider de time</c> pode
    /// alterar outro usuário, inclusive o papel dele. O caminho de correção é o mesmo aplicado em
    /// <see cref="CreateUser"/> — comparar com o claim <c>sub</c> e limitar por nível.
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
        var result = await _updateUserUseCase.UpdateAsync(id, req);
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
    /// Remoção física, sem exclusão lógica e sem como desfazer. Refresh tokens e registros de
    /// validação do usuário <b>não</b> são limpos — ver <c>DeleteUserUseCase</c>.
    /// </para>
    /// </summary>
    [Authorize(Policy = "Role:Admin")]
    [HttpDelete("/users/{id}")]
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DeleteUserResponse))]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var result = await _deleteUserUseCase.DeleteAsync(id);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// <c>POST /users/change-password</c> — troca a senha, exigindo a senha atual.
    ///
    /// <para>
    /// <b>Não confere se o <c>UserId</c> do corpo é o do token</b> (DT-16). A exigência da senha
    /// atual é o que limita o dano.
    /// </para>
    /// </summary>
    /// <returns>200, ou <b>401</b> — a falha esperada aqui é senha atual errada.</returns>
    [Authorize(Policy = "Role:Player")]
    [HttpPost("/users/change-password")]
    [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(ChangePasswordResponse))]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var result = await _changePasswordUseCase.ChangeAsync(req);
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
