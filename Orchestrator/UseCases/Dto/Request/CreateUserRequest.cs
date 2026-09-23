using System.ComponentModel.DataAnnotations;

namespace Orchestrator.UseCases.Dto.Request;

/// <summary>
/// Corpo de <c>POST /users</c> — cadastro administrativo de usuário.
///
/// <para>
/// Para auto-registro use <see cref="RegisterRequest"/>: lá o papel não vem do cliente.
/// </para>
///
/// <para>
/// <b>Como os atributos abaixo funcionam.</b> <c>[Required]</c>, <c>[EmailAddress]</c> e
/// <c>[Compare]</c> são verificados <b>automaticamente</b>, antes de o método do controller
/// executar, porque o controller é marcado com <c>[ApiController]</c>. Request inválido devolve 400
/// com a lista de erros sem passar pelo caso de uso. Ou seja: validação de <b>formato</b> mora aqui,
/// nos atributos; validação de <b>regra</b> (e-mail já existe, papel conhecido) mora no caso de uso.
/// </para>
/// </summary>
public class CreateUserRequest
{
    /// <summary>
    /// Nome de exibição. Espaços nas pontas são removidos pelo caso de uso.
    ///
    /// <para>
    /// O teto de 100 caracteres não é estética: sem limite, qualquer campo de texto livre é um
    /// vetor barato de consumo de banco e de poluição de log. Ver <c>docs/seguranca.md</c>.
    /// </para>
    /// </summary>
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// E-mail, que serve de login. <c>[EmailAddress]</c> confere apenas o formato — que o endereço
    /// exista, ou que ainda esteja livre, é outra história (a segunda é verificada no caso de uso).
    /// </summary>
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = null!;

    /// <summary>
    /// Senha em claro. Trafega no corpo da requisição, por HTTPS, e é convertida em hash logo no
    /// caso de uso — não é gravada nem registrada em log em ponto nenhum.
    ///
    /// <para>
    /// <c>[DataType(DataType.Password)]</c> não valida nada; serve para que o Swagger apresente o
    /// campo mascarado.
    /// </para>
    ///
    /// <para>
    /// <b>Mínimo de 8 caracteres.</b> É o piso do ASVS para senha escolhida por humano, e o único
    /// controle que o servidor pode impor sem transformar o cadastro num quebra-cabeça: com
    /// PBKDF2 de 100.000 iterações, uma senha de 8 caracteres já é cara de atacar; uma de 4 não é
    /// cara de jeito nenhum. O teto de 128 existe para que o custo do hash continue previsível.
    /// </para>
    /// </summary>
    [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = null!;

    /// <summary>
    /// Papel desejado, em português: <c>"jogador"</c>, <c>"jogador principal"</c>,
    /// <c>"lider de time"</c>, <c>"adm"</c> ou <c>"super adm"</c>.
    ///
    /// <para>
    /// <b>Nada aqui restringe o valor</b> — quem recusa papel desconhecido é o caso de uso, via
    /// <c>RoleHierarchy</c>. E quem impede um chamador de conceder papel acima da própria alçada é a
    /// policy no controller, não este DTO (DT-04).
    /// </para>
    /// </summary>
    [Required, StringLength(32)]
    public string Role { get; set; } = null!;

    /// <summary>
    /// Repetição da senha, para pegar erro de digitação.
    ///
    /// <para>
    /// <c>[Compare("Password")]</c> compara com a propriedade de nome <c>"Password"</c> — a
    /// referência é por <b>texto</b>, então renomear <see cref="Password"/> quebra esta validação
    /// <b>em silêncio</b>: o compilador não reclama e a conferência simplesmente para de acontecer.
    /// </para>
    /// </summary>
    [Compare("Password")]
    public string PasswordConfirmation { get; set; } = null!;

    /// <summary>Vínculos iniciais. Lista vazia é o caso normal.</summary>
    public List<UserAssignmentDto> Assignments { get; set; } = new();

    /// <summary>
    /// Quem está criando, para a auditoria.
    ///
    /// <para>
    /// <b>O que o cliente mandar aqui é descartado.</b> <c>UserController.CreateUser</c>
    /// sobrescreve este campo com o claim <c>sub</c> do token antes de chamar o caso de uso, e
    /// por isso ele deixou de ser <c>[Required]</c>: exigir um valor que será jogado fora só
    /// produz 400 em requisição correta. O campo continua no DTO para não quebrar clientes que
    /// já o enviam.
    /// </para>
    /// </summary>
    [StringLength(100)]
    public string CreatedBy { get; set; } = null!;
}
