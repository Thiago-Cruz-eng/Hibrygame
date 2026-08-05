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
    /// <summary>Nome de exibição. Espaços nas pontas são removidos pelo caso de uso.</summary>
    [Required]
    public string Name { get; set; } = null!;

    /// <summary>
    /// E-mail, que serve de login. <c>[EmailAddress]</c> confere apenas o formato — que o endereço
    /// exista, ou que ainda esteja livre, é outra história (a segunda é verificada no caso de uso).
    /// </summary>
    [Required, EmailAddress]
    public string Email { get; set; } = null!;

    /// <summary>
    /// Senha em claro. Trafega no corpo da requisição, por HTTPS, e é convertida em hash logo no
    /// caso de uso — não é gravada nem registrada em log em ponto nenhum.
    ///
    /// <para>
    /// <c>[DataType(DataType.Password)]</c> não valida nada; serve para que o Swagger apresente o
    /// campo mascarado.
    /// </para>
    /// </summary>
    [Required, DataType(DataType.Password)]
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
    [Required]
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
    /// <b>Vem do cliente</b>, e não do token de quem chamou — o servidor acredita no que for
    /// enviado. Isso significa que o campo de auditoria pode ser preenchido com qualquer valor.
    /// Ver DT-16 em <c>docs/debito-tecnico.md</c>: a saída é derivar isto do claim <c>sub</c>, como
    /// <c>ValidationController</c> já faz para a identidade.
    /// </para>
    /// </summary>
    [Required]
    public string CreatedBy { get; set; } = null!;
}
