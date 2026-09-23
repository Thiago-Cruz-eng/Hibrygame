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
/// <b>E o tempo de resposta também não distingue os dois casos.</b> Conferir uma senha custa
/// PBKDF2 de 100.000 iterações — dezenas de milissegundos. Se o ramo "e-mail não existe" voltasse
/// direto, ele responderia em uma fração desse tempo, e medir a diferença diria quais e-mails
/// estão cadastrados <b>mesmo com a mensagem idêntica</b>. Por isso, quando o usuário não existe,
/// a verificação é feita assim mesmo, contra um hash fixo que nenhuma senha satisfaz. Ver
/// <see cref="DummyPasswordHash"/>.
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
    /// <summary>
    /// Hash de 32 bytes contra o qual se confere a senha quando o e-mail não existe.
    ///
    /// <para>
    /// Não é o hash de senha nenhuma — é um valor constante, e nenhuma entrada o satisfaz. O que
    /// importa é que <c>Verify</c> execute as mesmas 100.000 iterações do ramo legítimo, para que
    /// os dois caminhos gastem o mesmo tempo. O resultado é descartado.
    /// </para>
    ///
    /// <para>
    /// Precisa ser Base64 válido de 32 bytes porque é isso que <c>SecureHashingService.Verify</c>
    /// decodifica antes de comparar; um texto qualquer estouraria <c>FormatException</c> e
    /// transformaria a defesa num erro 500.
    /// </para>
    /// </summary>
    private const string DummyPasswordHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    /// <summary>Salt de 16 bytes, pelo mesmo motivo de <see cref="DummyPasswordHash"/>.</summary>
    private const string DummyPasswordSalt = "AAAAAAAAAAAAAAAAAAAAAA==";

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
            {
                // Gasta o mesmo tempo do ramo legítimo. O retorno é descartado de propósito: o que
                // interessa é o custo, não o resultado. Ver DummyPasswordHash.
                _hashingService.Verify(req.Password, DummyPasswordHash, DummyPasswordSalt);

                // Sem e-mail no log: registrar a tentativa é útil; registrar qual conta alguém
                // está tentando adivinhar transforma o log num diretório de e-mails.
                _logger.LogWarning("Failed login attempt for unknown account");
                return new LoginResponse { Message = "Invalid credentials", Success = false };
            }

            if (!_hashingService.Verify(req.Password, user.PasswordHash, user.Salt))
            {
                // Aqui a conta existe, então o id pode entrar: é o que permite ver força bruta
                // concentrada num usuário. Senha nunca entra em log, em ramo nenhum.
                _logger.LogWarning("Failed login attempt for user {UserId}", user.Id);
                return new LoginResponse { Message = "Invalid credentials", Success = false };
            }

            var accessTokenResult = _tokenService.CreateAccessToken(user);
            var refreshTokenResult = _tokenService.CreateRefreshToken(user);

            // CreateRefreshToken monta a entidade mas não a grava — persistir é responsabilidade
            // de quem chama, e é aqui.
            await _refreshTokenRepository.Save(refreshTokenResult.Token);

            // Registro de validação de sessão. O token é gravado como resumo SHA-256 pelo
            // ValidationService — a coleção não guarda mais sessões prontas para uso (era a
            // DT-07). Sala e cor entram como null: só serão conhecidas quando o jogador entrar
            // numa sala pelo lobby.
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
