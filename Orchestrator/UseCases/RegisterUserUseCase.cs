using Microsoft.Extensions.Logging;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Security.Authorization;

namespace Orchestrator.UseCases;

/// <summary>
/// Auto-registro: cria a conta e ja devolve sessao.
///
/// Existe para separar duas coisas que estavam na mesma porta:
///
///   POST /register  anonimo, papel e autor DECIDIDOS AQUI, devolve token
///   POST /users     exige Role:Admin, aceita papel pelo corpo
///
/// Antes so existia o segundo, anonimo, aceitando <c>Role</c> pelo corpo do request —
/// qualquer pessoa na internet criava um "super adm" (DT-04). E o frontend usava esse
/// endpoint para cadastro, recebia uma resposta sem token e navegava para o lobby como se
/// tivesse sessao.
///
/// Compoe CreateUserUseCase e LoginAsyncUseCase em vez de repetir a emissao de token: um
/// caminho so para gerar access token, refresh token e registro de validacao.
/// </summary>
public class RegisterUserUseCase
{
    /// <summary>Papel de quem se auto-registra. Nao vem do cliente.</summary>
    private const RoleLevel SelfRegisteredRole = RoleLevel.Player;

    /// <summary>Autor na auditoria. Tambem nao vem do cliente.</summary>
    private const string SelfRegisteredBy = "self-registration";

    private readonly CreateUserUseCase _createUser;
    private readonly LoginAsyncUseCase _login;
    private readonly ILogger<RegisterUserUseCase> _logger;

    public RegisterUserUseCase(
        CreateUserUseCase createUser,
        LoginAsyncUseCase login,
        ILogger<RegisterUserUseCase> logger)
    {
        _createUser = createUser;
        _login = login;
        _logger = logger;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest req)
    {
        try
        {
            var created = await _createUser.CreateAsync(new CreateUserRequest
            {
                Name = req.Name,
                Email = req.Email,
                Password = req.Password,
                PasswordConfirmation = req.PasswordConfirmation,
                Role = RoleHierarchy.NormalizeRole(SelfRegisteredRole),
                CreatedBy = SelfRegisteredBy,
                Assignments = []
            });

            if (!created.Success)
                return new RegisterResponse { Success = false, Message = created.Message };

            // A conta existe; agora emite sessao pelo mesmo caminho do login normal.
            var session = await _login.LoginAsync(new LoginRequest
            {
                Email = req.Email,
                Password = req.Password
            });

            if (!session.Success)
            {
                // Conta criada mas token nao emitido. Nao e para acontecer — se acontecer,
                // o usuario consegue entrar pelo login, entao a mensagem diz isso.
                _logger.LogError(
                    "Usuario {UserId} foi criado mas a sessao nao pode ser emitida: {Message}",
                    created.UserId, session.Message);

                return new RegisterResponse
                {
                    Success = true,
                    UserId = created.UserId,
                    Name = req.Name.Trim(),
                    Email = req.Email.Trim().ToLowerInvariant(),
                    Role = RoleHierarchy.NormalizeRole(SelfRegisteredRole),
                    Message = "Account created. Please log in."
                };
            }

            return new RegisterResponse
            {
                Success = true,
                Message = "Account created",
                UserId = session.UserId ?? created.UserId,
                Name = req.Name.Trim(),
                Email = session.Email,
                Role = session.Role,
                AccessToken = session.AccessToken,
                RefreshToken = session.RefreshToken,
                ExpiresAt = session.ExpiresAt
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while registering user.");
            return new RegisterResponse { Success = false, Message = "Registration failed" };
        }
    }
}
