using Orchestrator.Domain;
using Orchestrator.Infra.Interfaces;
using Orchestrator.UseCases.Dto.Request;
using Orchestrator.UseCases.Dto.Response;
using Orchestrator.UseCases.Interfaces;
using Orchestrator.UseCases.Security.Authorization;

namespace Orchestrator.UseCases;

/// <summary>
/// Cria um usuário com papel escolhido por quem está chamando. É o caso de uso por trás de
/// <c>POST /users</c>.
///
/// <para>
/// <b>Não confundir com <see cref="RegisterUserUseCase"/>.</b> Aquele é o auto-registro: decide o
/// papel por conta própria (sempre <c>jogador</c>) e já devolve sessão. Este aceita o papel pelo
/// request e não emite token — serve para cadastro administrativo.
/// </para>
///
/// <para>
/// <b>Superfície de segurança (DT-04):</b> este caso de uso não confere se quem chama tem alçada
/// para conceder o papel pedido. Quem barra isso é a policy no controller. Se algum endpoint novo
/// passar a chamá-lo, a autorização tem de ser garantida lá — não há rede de proteção aqui.
/// </para>
///
/// <para>
/// <b>Anatomia de um caso de uso neste projeto</b> — este arquivo serve de molde:
/// </para>
/// <list type="number">
///   <item><description>uma classe por ação, injeção só por construtor, sem herança;</description></item>
///   <item><description>um método público que recebe o <c>{Ação}Request</c> e devolve o
///   <c>{Ação}Response</c>;</description></item>
///   <item><description>o corpo inteiro dentro de <c>try/catch</c>;</description></item>
///   <item><description><b>nunca deixa exceção subir</b> para o controller: falha vira
///   <c>Success = false</c> com mensagem;</description></item>
///   <item><description>a exceção é registrada com <c>ILogger</c> antes de virar resposta, senão a
///   causa real se perde.</description></item>
/// </list>
/// </summary>
public class CreateUserUseCase
{
    private readonly IUserRepositoryNoSql _userRepository;
    private readonly ISecureHashingService _hashingService;
    private readonly ILogger<CreateUserUseCase> _logger;

    public CreateUserUseCase(
        IUserRepositoryNoSql userRepository,
        ISecureHashingService hashingService,
        ILogger<CreateUserUseCase> logger)
    {
        _userRepository = userRepository;
        _hashingService = hashingService;
        _logger = logger;
    }

    /// <summary>
    /// Cria o usuário, se o e-mail estiver livre e o papel for válido.
    /// </summary>
    /// <returns>
    /// <c>Success = true</c> com o <c>UserId</c> gerado; ou <c>Success = false</c> com o motivo —
    /// e-mail já cadastrado, papel inválido, ou falha inesperada.
    /// </returns>
    public async Task<CreateUserResponse> CreateAsync(CreateUserRequest req)
    {
        try
        {
            var normalizedEmail = EmailNormalization.Normalize(req.Email);

            // Unicidade do e-mail é garantida aqui, e só aqui: não existe índice único no
            // MongoDB. Isso deixa uma janela de corrida entre esta consulta e o Save mais abaixo —
            // duas requisições simultâneas com o mesmo e-mail passam as duas. Fechar de verdade
            // exige índice único na coleção.
            var existing = await _userRepository.FindByFilter(user => user.Email == normalizedEmail);
            if (existing.Any())
                return new CreateUserResponse { Message = "User already has a account", Success = false };

            // A entidade aceita qualquer texto em Role — é este ponto que restringe aos cinco
            // papéis válidos.
            if (!RoleHierarchy.TryGetLevel(req.Role, out var roleLevel))
                return new CreateUserResponse { Message = "Invalid role", Success = false };

            // Ida e volta pelo nível: aceita "Adm" ou " adm " na entrada e grava sempre a forma
            // canônica, para que a comparação de papel no banco seja previsível.
            var normalizedRole = RoleHierarchy.NormalizeRole(roleLevel);

            // A senha em claro morre aqui: só o hash e o salt seguem adiante.
            var (hash, salt) = _hashingService.HashValue(req.Password);

            var assignments = req.Assignments
                .Select(assignment => assignment.ToDomain(req.CreatedBy))
                .ToList();

            var user = User.Create(
                name: req.Name.Trim(),
                email: normalizedEmail,
                role: normalizedRole,
                passwordHash: hash,
                salt: salt,
                assignments: assignments,
                createdBy: req.CreatedBy.Trim());

            await _userRepository.Save(user);

            // O id existe desde User.Create — BaseEntity o gera na construção —, então pode ser
            // devolvido sem uma segunda ida ao banco.
            return new CreateUserResponse
            {
                Success = true,
                Message = "User created",
                UserId = user.Id.ToString()
            };
        }
        catch (Exception e)
        {
            // Loga a exceção real e devolve mensagem genérica: detalhe de exceção em resposta HTTP
            // conta ao cliente como o sistema é feito por dentro.
            _logger.LogError(e, "Error while creating user.");
            return new CreateUserResponse { Message = "Same error happen", Success = false };
        }
    }
}
