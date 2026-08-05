using Orchestrator.Infra.Utils;

namespace Orchestrator.Domain;

/// <summary>
/// Um usuário da plataforma: quem faz login, recebe papel e joga.
///
/// <para>
/// <b>Esta é a entidade de referência do projeto.</b> Ela cumpre o contrato descrito em
/// <see cref="BaseEntity"/> por inteiro — setters protegidos, construtor para o Mongo,
/// <see cref="Create"/> como única porta de criação, mutadores nomeados devolvendo <c>this</c>,
/// auditoria em cada mudança e <c>[CollectionName]</c>. Ao criar entidade nova, copie a
/// estrutura daqui.
/// </para>
///
/// <para>
/// <b>Onde a entidade termina.</b> Ela guarda estado e registra quem mudou o quê. Ela não
/// valida regra de negócio, não hasheia senha e não checa autorização — isso é dos casos de uso
/// e de <c>SecureHashingService</c>. Em particular, <see cref="Create"/> aceita qualquer texto
/// em <c>role</c>: quem garante que o papel é um dos cinco válidos é
/// <c>CreateUserUseCase</c>, comparando com <c>RoleHierarchy</c>.
/// </para>
/// </summary>
[CollectionName(nameof(User))]
public class User : BaseEntity
{
    /// <summary>Nome de exibição. Não é identificador — quem identifica é <see cref="Email"/>.</summary>
    public string Name { get; protected set; } = null!;

    /// <summary>
    /// E-mail, e na prática o login do usuário.
    ///
    /// <para>
    /// Deve ser único, mas <b>a unicidade não é garantida aqui</b> nem por índice no MongoDB:
    /// quem checa é <c>CreateUserUseCase</c>, procurando o e-mail antes de inserir. Isso deixa
    /// uma janela de corrida entre a consulta e a inserção — duas requisições simultâneas com o
    /// mesmo e-mail podem passar as duas.
    /// </para>
    ///
    /// <para>
    /// A normalização (minúsculas, sem espaços nas pontas) é feita pelo caso de uso antes de
    /// chegar aqui, não por esta classe.
    /// </para>
    /// </summary>
    public string Email { get; protected set; } = null!;

    /// <summary>
    /// Papel do usuário na hierarquia de autorização, gravado como texto em português:
    /// <c>"jogador"</c>, <c>"jogador principal"</c>, <c>"lider de time"</c>, <c>"adm"</c> ou
    /// <c>"super adm"</c>.
    ///
    /// <para>
    /// É <c>string</c> e não <c>enum</c> porque o valor vai para o banco e para o claim do JWT
    /// nessa forma. A tradução para nível numérico comparável vive em <c>RoleHierarchy</c>, e é
    /// o que permite a uma policy exigir "pelo menos <c>adm</c>".
    /// </para>
    ///
    /// <para>
    /// Nada nesta classe restringe o valor a esses cinco.
    /// </para>
    /// </summary>
    public string Role { get; protected set; } = null!;

    /// <summary>
    /// Hash da senha — nunca a senha. Produzido por <c>SecureHashingService</c> (PBKDF2), que é
    /// também quem verifica.
    /// </summary>
    public string PasswordHash { get; protected set; } = null!;

    /// <summary>
    /// Salt usado para gerar <see cref="PasswordHash"/>. Único por usuário e trocado a cada
    /// mudança de senha, o que faz duas senhas iguais produzirem hashes diferentes.
    /// </summary>
    public string Salt { get; protected set; } = null!;

    /// <summary>
    /// Marca que a senha precisa ser trocada no próximo acesso — usado quando a senha foi
    /// definida por outra pessoa (cadastro administrativo, reset).
    ///
    /// <para>
    /// <see cref="ChangePassword"/> zera esta marca automaticamente: trocar a senha é
    /// exatamente o que ela pedia.
    /// </para>
    /// </summary>
    public bool MustChangePassword { get; protected set; }

    /// <summary>
    /// Vínculos do usuário com empresas e contextos de hierarquia. Ver
    /// <see cref="UserAssignment"/>.
    ///
    /// <para>
    /// Note que <see cref="Role"/> continua sendo o papel principal, usado pela autorização. Os
    /// vínculos são informação adicional e não substituem o papel.
    /// </para>
    /// </summary>
    public List<UserAssignment> Assignments { get; protected set; } = new();

    /// <summary>Quem criou e quando. Preenchido por <see cref="Create"/>, nunca depois.</summary>
    public CreationInformation CreationInformations { get; protected set; } = null!;

    /// <summary>
    /// Última alteração. <c>null</c> em usuário que nunca foi alterado — estado legítimo, não
    /// dado faltando.
    /// </summary>
    public ModificationInformation? ModificationInformations { get; protected set; }

    /// <summary>
    /// Construtor vazio para o MongoDB desserializar. Não chame — use <see cref="Create"/>.
    /// </summary>
    protected User() { }

    private User(
        string name,
        string email,
        string role,
        string passwordHash,
        string salt,
        List<UserAssignment> assignments,
        string createdBy)
    {
        Name = name;
        Email = email;
        Role = role;
        PasswordHash = passwordHash;
        Salt = salt;
        MustChangePassword = false;

        // Cópia da lista, e não a referência recebida: guardar a lista de quem chamou deixaria
        // um código de fora capaz de acrescentar vínculo ao usuário depois, sem passar por
        // ChangeAssignments — e portanto sem registrar auditoria.
        Assignments = new List<UserAssignment>(assignments);

        CreationInformations = new CreationInformation(createdBy);
    }

    /// <summary>
    /// Cria um usuário. Única forma de obter uma instância nova.
    ///
    /// <para>
    /// <b>Não valida nada.</b> Espera receber os valores já prontos: e-mail normalizado, papel
    /// entre os válidos e senha já hasheada. Quem cuida disso é <c>CreateUserUseCase</c>.
    /// </para>
    /// </summary>
    /// <param name="passwordHash">Hash da senha, nunca a senha em claro.</param>
    /// <param name="salt">Salt correspondente a esse hash.</param>
    /// <param name="createdBy">Quem está criando, para a auditoria.</param>
    public static User Create(
        string name,
        string email,
        string role,
        string passwordHash,
        string salt,
        List<UserAssignment> assignments,
        string createdBy)
        => new(name, email, role, passwordHash, salt, assignments, createdBy);

    /// <summary>
    /// Exige troca de senha no próximo acesso.
    /// </summary>
    /// <returns>A própria instância, para permitir encadear mutadores.</returns>
    public User MarkPasswordChangeRequired(string modifiedBy)
    {
        MustChangePassword = true;
        ModificationInformations = new ModificationInformation(modifiedBy);
        return this;
    }

    /// <summary>
    /// Substitui a lista de vínculos inteira — não acrescenta. Para incluir um vínculo, monte a
    /// lista nova a partir da atual e passe o resultado.
    /// </summary>
    public User ChangeAssignments(List<UserAssignment> assignments, string modifiedBy)
    {
        // Cópia pelo mesmo motivo do construtor.
        Assignments = new List<UserAssignment>(assignments);
        ModificationInformations = new ModificationInformation(modifiedBy);
        return this;
    }

    /// <summary>Troca o nome de exibição.</summary>
    public User ChangeName(string name, string modifiedBy)
    {
        Name = name;
        ModificationInformations = new ModificationInformation(modifiedBy);
        return this;
    }

    /// <summary>
    /// Troca o e-mail. Como o e-mail é o login, isto muda por onde o usuário entra — e a
    /// unicidade continua sendo responsabilidade do caso de uso.
    /// </summary>
    public User ChangeEmail(string email, string modifiedBy)
    {
        Email = email;
        ModificationInformations = new ModificationInformation(modifiedBy);
        return this;
    }

    /// <summary>
    /// Troca o papel, e com isso o que o usuário pode fazer.
    ///
    /// <para>
    /// Não valida o valor recebido, e não confere se quem pede tem alçada para promover. As duas
    /// checagens são do caso de uso.
    /// </para>
    /// </summary>
    public User ChangeRole(string role, string modifiedBy)
    {
        Role = role;
        ModificationInformations = new ModificationInformation(modifiedBy);
        return this;
    }

    /// <summary>
    /// Grava senha nova e limpa <see cref="MustChangePassword"/>.
    /// </summary>
    /// <param name="passwordHash">Hash da senha nova.</param>
    /// <param name="salt">
    /// Salt novo. Sempre um novo — reaproveitar o anterior anularia parte do ganho de ter salt.
    /// </param>
    public User ChangePassword(string passwordHash, string salt, string modifiedBy)
    {
        PasswordHash = passwordHash;
        Salt = salt;
        MustChangePassword = false;
        ModificationInformations = new ModificationInformation(modifiedBy);
        return this;
    }

    /// <summary>
    /// Igualdade por <b>identidade</b>: dois usuários são o mesmo quando têm o mesmo
    /// <c>Id</c>, independentemente de nome, e-mail ou papel.
    ///
    /// <para>
    /// É o que se quer de uma entidade — a instância recém-lida do banco e a que está em
    /// memória representam o mesmo usuário mesmo que uma esteja desatualizada. Comparar campo a
    /// campo faria de uma alteração de nome um "outro usuário".
    /// </para>
    /// </summary>
    public override bool Equals(object? obj)
        => obj is User other && Id == other.Id;

    /// <summary>
    /// Derivado só do <c>Id</c>, para casar com <see cref="Equals"/>. Os dois têm de andar
    /// juntos: se um considerasse mais campos que o outro, o usuário sumiria de dentro de um
    /// <c>Dictionary</c> ou <c>HashSet</c> ao ser alterado.
    /// </summary>
    public override int GetHashCode()
        => HashCode.Combine(Id);
}
