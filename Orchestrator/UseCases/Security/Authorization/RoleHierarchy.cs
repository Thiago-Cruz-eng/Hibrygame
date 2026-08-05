namespace Orchestrator.UseCases.Security.Authorization;

/// <summary>
/// Os papéis do sistema como valores <b>ordenáveis</b>.
///
/// <para>
/// Os números não são decorativos: é o que permite escrever "pelo menos <c>Admin</c>" como
/// <c>nivel &gt;= RoleLevel.Admin</c>. Sem uma ordem, cada policy teria de listar todos os papéis
/// aceitos, e acrescentar um papel novo obrigaria a revisar todas elas.
/// </para>
///
/// <para>
/// <b>Ao acrescentar um papel</b>, escolha o número pela posição dele na hierarquia e não pelo
/// próximo valor livre — inserir <c>Moderator</c> entre <c>TeamLeader</c> (3) e <c>Admin</c> (4)
/// exige renumerar. Os valores não são persistidos (o banco guarda o texto em português), então
/// renumerar é seguro; o que não é seguro é deixar a ordem numérica divergir da hierarquia real.
/// </para>
/// </summary>
public enum RoleLevel
{
    /// <summary>Jogador comum. Menor nível — é o que o hub <c>/chesshub</c> exige.</summary>
    Player = 1,

    /// <summary>Jogador principal.</summary>
    MainPlayer = 2,

    /// <summary>Líder de time.</summary>
    TeamLeader = 3,

    /// <summary>Administrador.</summary>
    Admin = 4,

    /// <summary>Super administrador. Maior nível — passa em qualquer policy.</summary>
    SuperAdmin = 5
}

/// <summary>
/// Tradução entre o papel como <b>texto</b> (que é como ele vive no MongoDB e no claim do JWT) e
/// o papel como <see cref="RoleLevel"/> (que é como ele se compara).
///
/// <para>
/// <b>Fonte única dessa correspondência.</b> Se você precisar decidir se um texto é um papel
/// válido, ou comparar dois papéis, passe por aqui — não escreva a comparação de strings à mão em
/// outro lugar.
/// </para>
/// </summary>
public static class RoleHierarchy
{
    /// <summary>
    /// Os cinco papéis reconhecidos, em português, como estão gravados no banco.
    ///
    /// <para>
    /// <c>OrdinalIgnoreCase</c> faz <c>"Adm"</c> e <c>"adm"</c> serem o mesmo papel — tolerância
    /// deliberada, porque o valor pode ter sido gravado com maiúscula em algum ponto.
    /// <b>Ordinal</b> e não cultural: comparação sensível à cultura pode mudar de resultado
    /// conforme o idioma do servidor, e autorização não pode depender disso.
    /// </para>
    ///
    /// <para>
    /// As chaves não têm acento ("lider", não "líder") porque é assim que estão gravadas. Corrigir
    /// exigiria migrar os documentos existentes.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, RoleLevel> RoleMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jogador"] = RoleLevel.Player,
        ["jogador principal"] = RoleLevel.MainPlayer,
        ["lider de time"] = RoleLevel.TeamLeader,
        ["adm"] = RoleLevel.Admin,
        ["super adm"] = RoleLevel.SuperAdmin
    };

    /// <summary>
    /// Converte o texto do papel no nível correspondente.
    ///
    /// <para>
    /// Segue o padrão <c>Try...</c> do .NET: devolve <c>false</c> em vez de lançar quando o texto
    /// não é um papel conhecido. É o que se quer, porque o valor pode vir de um token emitido por
    /// outra versão do sistema — papel desconhecido é "sem permissão", não erro de servidor.
    /// </para>
    /// </summary>
    /// <param name="role">O papel como texto. Espaços nas pontas são ignorados.</param>
    /// <param name="level">O nível, quando reconhecido.</param>
    /// <returns><c>false</c> quando <paramref name="role"/> não é um dos cinco papéis.</returns>
    public static bool TryGetLevel(string role, out RoleLevel level)
        => RoleMap.TryGetValue(role.Trim(), out level);

    /// <summary>
    /// Caminho inverso: o texto canônico de um <see cref="RoleLevel"/>. Usado para gravar o papel
    /// de forma consistente, independentemente de como ele foi digitado na entrada.
    ///
    /// <para>
    /// <b>Cuidado com o caso <c>default</c>:</b> um valor de enum fora dos cinco (o que é possível
    /// em C#, já que um <c>enum</c> aceita qualquer inteiro por conversão) não estoura — devolve
    /// <c>"jogador"</c>, o menor privilégio. Falhar para o lado seguro é intencional, mas
    /// significa que um valor inválido passa em silêncio, sem log.
    /// </para>
    /// </summary>
    public static string NormalizeRole(RoleLevel level) => level switch
    {
        RoleLevel.Player => "jogador",
        RoleLevel.MainPlayer => "jogador principal",
        RoleLevel.TeamLeader => "lider de time",
        RoleLevel.Admin => "adm",
        RoleLevel.SuperAdmin => "super adm",
        _ => "jogador"
    };
}
