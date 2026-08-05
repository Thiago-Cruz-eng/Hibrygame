using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.UseCases;

/// <summary>
/// Autentica por e-mail e senha e devolve a sessão: access token, refresh token e os dados que o
/// frontend usa para montar a tela. Caso de uso de <c>POST /login</c>.
///
/// <para>
/// <b>Mensagem de erro deliberadamente vaga.</b> E-mail inexistente e senha errada devolvem
/// exatamente a mesma resposta, <c>"Invalid credentials"</c>. Distinguir os dois casos contaria a
/// quem tenta adivinhar <b>quais e-mails existem</b> na base — o que transforma o login num
/// verificador de contas e é o primeiro passo de um ataque direcionado.
/// </para>
///
/// <para>
/// Este é o caso de uso com mais dependências do projeto (seis), porque login é onde muita coisa
/// se encontra: achar o usuário, conferir a senha, emitir os dois tokens, persistir o refresh e
/// registrar a validação de sessão.
/// </para>
/// </summary>
public class LoginAsyncUseCase
{
    private readonly IValidationService _validationService;
    private readonly ILogger<LoginAsyncUseCase> _logger;
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly IRefreshTokenRepositoryNoSql _refreshTokenRepository;
    private readonly ISecureHashingService _hashingService;
    private readonly ITokenService _tokenService;

    public LoginAsyncUseCase(
        IValidationService validationService,
        ILogger<LoginAsyncUseCase> logger,
        IUserRepositoryNoSql userRepository,
        IRefreshTokenRepositoryNoSql refreshTokenRepository,
        ISecureHashingService hashingService,
        ITokenService tokenService)
    {
        _validationService = validationService;
        _logger = logger;
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _hashingService = hashingService;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Confere as credenciais e emite a sessão.
    /// </summary>
    /// <returns>
    /// Em caso de sucesso, o access token (para o cabeçalho <c>Authorization</c> e para o
    /// <c>?access_token=</c> do hub), o refresh token <b>em claro</b> — única vez em que ele existe
    /// — e os dados do usuário. Em caso de falha, <c>Success = false</c>.
    /// </returns>
    public async Task<LoginResponse> LoginAsync(LoginRequest req)
    {
        try
        {
            // Normaliza igual ao cadastro: sem isto, um e-mail digitado com maiúscula não encontra
            // o usuário e o login falha sem motivo aparente.
            var normalizedEmail = EmailNormalization.Normalize(req.Email);
            var users = await _userRepository.FindByFilter(user => user.Email == normalizedEmail);
            var user = users.FirstOrDefault();

            // Mesma mensagem nos dois ramos abaixo, de propósito — ver a nota da classe.
            if (user is null)
                return new LoginResponse { Message = "Invalid credentials", Success = false };

            if (!_hashingService.Verify(req.Password, user.PasswordHash, user.Salt))
                return new LoginResponse { Message = "Invalid credentials", Success = false };

            var accessTokenResult = _tokenService.CreateAccessToken(user);
            var refreshTokenResult = _tokenService.CreateRefreshToken(user);

            // CreateRefreshToken monta a entidade mas não a grava — persistir é responsabilidade
            // de quem chama, e é aqui.
            await _refreshTokenRepository.Save(refreshTokenResult.Token);

            // Registro de validação de sessão. Guarda o access token EM CLARO (DT-07) e serve à
            // autorização paralela do subdomínio Validation. Sala e cor entram como null: só
            // serão conhecidas quando o jogador entrar numa sala pelo lobby.
            await _validationService.CreateValidation(new ValidationDto
            {
                AcessToken = accessTokenResult.Token,
                Room = null,
                UserId = user.Id.ToString(),
                PieceColor = null,
                UserEmail = user.Email
            });

            return new LoginResponse
            {
                Success = true,
                AccessToken = accessTokenResult.Token,

                // O valor em claro do refresh token. Depois desta resposta ele não existe em
                // lugar nenhum — no banco só há o hash.
                RefreshToken = refreshTokenResult.RawToken,

                ExpiresAt = accessTokenResult.ExpiresAt,
                Email = user.Email,
                UserId = user.Id.ToString(),
                Role = user.Role,

                // O frontend usa isto para mandar o usuário à tela de troca de senha em vez do
                // lobby, quando a senha foi definida por outra pessoa.
                MustChangePassword = user.MustChangePassword,

                Message = "User logged"
            };
        }
        catch (Exception e)
        {
            // "Login failed", e não "Invalid credentials": aqui houve falha do sistema, não
            // credencial errada. A distinção importa para diagnosticar — e o motivo real fica no
            // log, não na resposta.
            _logger.LogError(e, "Error while logging user in.");
            return new LoginResponse { Message = "Login failed", Success = false };
        }
    }
}
