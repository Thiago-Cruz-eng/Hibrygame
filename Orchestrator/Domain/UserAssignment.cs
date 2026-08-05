namespace Orchestrator.Domain;

/// <summary>
/// Vínculo de um usuário com um time, um papel dentro desse time e os nós de hierarquia a que
/// ele tem acesso. Fica gravado dentro do documento de <see cref="User"/>, em
/// <see cref="User.Assignments"/> — não é coleção própria e por isso não herda de
/// <see cref="BaseEntity"/>.
///
/// <para>
/// <b>ATENÇÃO — os parâmetros <c>createdBy</c> e <c>modifiedBy</c> desta classe não fazem nada.</b>
/// Todos os métodos aqui os recebem e nenhum os usa: a classe não tem campo de auditoria onde
/// gravá-los. Até o refactor de 2026-08-03 os comentários afirmavam "updates audit metadata" em
/// cada um deles, o que era simplesmente falso.
/// </para>
///
/// <para>
/// Consequência prática: <b>alteração de vínculo não deixa rastro</b>. Se você precisa saber quem
/// mudou o time ou o papel de alguém, a informação não existe. Quando esse rastro passar a ser
/// necessário, o caminho é acrescentar
/// <see cref="CreationInformation"/>/<see cref="ModificationInformation"/> aqui — como
/// <see cref="User"/> faz — e então os parâmetros passam a ter para onde ir. Enquanto isso não
/// acontecer, eles estão mantidos apenas para não quebrar os chamadores e os testes.
/// </para>
///
/// <para>
/// Note também que este vínculo é <b>informativo</b>: a autorização do sistema decide por
/// <see cref="User.Role"/>, não pelo <see cref="RoleName"/> daqui. Mudar o papel no vínculo não
/// concede permissão nenhuma.
/// </para>
/// </summary>
public class UserAssignment
{
    /// <summary>Nome do time, para exibição.</summary>
    public string TeamName { get; protected set; } = null!;

    /// <summary>Identificador do time. Texto livre — nada aqui garante que o time exista.</summary>
    public string TeamId { get; protected set; } = null!;

    /// <summary>
    /// Nome do papel dentro deste time, para exibição.
    ///
    /// <para>
    /// <b>Não é</b> o papel que a autorização usa. Quem manda em permissão é
    /// <see cref="User.Role"/>, comparado por <c>RoleHierarchy</c>.
    /// </para>
    /// </summary>
    public string RoleName { get; protected set; } = null!;

    /// <summary>Identificador do papel. Texto livre, sem validação.</summary>
    public string RoleId { get; protected set; } = null!;

    /// <summary>Nós de hierarquia a que este vínculo dá acesso. Ver <see cref="HierarchyNode"/>.</summary>
    public List<HierarchyNode> HierarchyNodes { get; protected set; } = new();

    /// <summary>Construtor vazio para o MongoDB desserializar. Use <see cref="Create"/>.</summary>
    protected UserAssignment() { }

    private UserAssignment(
        string teamName,
        string teamId,
        string roleName,
        string roleId,
        List<HierarchyNode> hierarchyNodes,
        string createdBy)
    {
        TeamName = teamName;
        TeamId = teamId;
        RoleName = roleName;
        RoleId = roleId;

        // Cópia da lista, não a referência recebida: guardá-la deixaria quem chamou capaz de
        // alterar os nós depois, por fora dos mutadores.
        HierarchyNodes = new List<HierarchyNode>(hierarchyNodes);

        // `createdBy` não é usado — ver a nota da classe.
    }

    /// <summary>
    /// Cria um vínculo. Nenhum dos identificadores é validado.
    /// </summary>
    /// <param name="createdBy">
    /// <b>Ignorado.</b> Sem efeito nenhum — ver a nota da classe.
    /// </param>
    public static UserAssignment Create(
        string teamName,
        string teamId,
        string roleName,
        string roleId,
        List<HierarchyNode> hierarchyNodes,
        string createdBy)
        => new(teamName, teamId, roleName, roleId, hierarchyNodes, createdBy);

    /// <summary>
    /// Troca o time do vínculo.
    /// </summary>
    /// <param name="modifiedBy"><b>Ignorado</b> — ver a nota da classe.</param>
    /// <returns>A própria instância, para encadear.</returns>
    public UserAssignment ChangeTeam(
        string teamName,
        string teamId,
        string modifiedBy)
    {
        TeamName = teamName;
        TeamId = teamId;
        return this;
    }

    /// <summary>
    /// Troca o papel <b>dentro do time</b>. Não altera permissão nenhuma — ver a nota em
    /// <see cref="RoleName"/>.
    /// </summary>
    /// <param name="modifiedBy"><b>Ignorado</b> — ver a nota da classe.</param>
    public UserAssignment ChangeRole(
        string roleName,
        string roleId,
        string modifiedBy)
    {
        RoleName = roleName;
        RoleId = roleId;
        return this;
    }

    /// <summary>
    /// Substitui a lista de nós inteira. Para acrescentar um único nó use
    /// <see cref="AddHierarchyNode"/>.
    /// </summary>
    /// <param name="modifiedBy"><b>Ignorado</b> — ver a nota da classe.</param>
    public UserAssignment ChangeHierarchy(
        List<HierarchyNode> hierarchyNodes,
        string modifiedBy)
    {
        HierarchyNodes = new List<HierarchyNode>(hierarchyNodes);
        return this;
    }

    /// <summary>
    /// Acrescenta um nó, sem duplicar.
    ///
    /// <para>
    /// A checagem de duplicata funciona por <b>valor</b>, não por referência, porque
    /// <see cref="HierarchyNode"/> é um <c>record</c>: dois nós com o mesmo id e o mesmo nome são
    /// considerados iguais mesmo sendo instâncias distintas. Se algum dia o <c>record</c> virar
    /// <c>class</c>, esta deduplicação para de funcionar silenciosamente.
    /// </para>
    /// </summary>
    /// <param name="modifiedBy"><b>Ignorado</b> — ver a nota da classe.</param>
    public UserAssignment AddHierarchyNode(
        HierarchyNode node,
        string modifiedBy)
    {
        if (!HierarchyNodes.Contains(node))
            HierarchyNodes.Add(node);
        return this;
    }

    /// <summary>
    /// Remove o nó de <paramref name="nodeId"/>. Nó inexistente não é erro: o método simplesmente
    /// não faz nada.
    ///
    /// <para>
    /// A remoção é por <b>id</b>, e não pelo valor inteiro do nó — então o nome do nó não precisa
    /// bater.
    /// </para>
    /// </summary>
    /// <param name="modifiedBy"><b>Ignorado</b> — ver a nota da classe.</param>
    public UserAssignment RemoveHierarchyNode(
        string nodeId,
        string modifiedBy)
    {
        var node = HierarchyNodes.FirstOrDefault(n => n.NodeId == nodeId);
        if (node != null)
        {
            HierarchyNodes.Remove(node);
        }
        return this;
    }

    /// <summary>
    /// Forma legível do vínculo, para log e diagnóstico. Não é formato de contrato: nada faz
    /// parsing deste texto, então mudá-lo é seguro.
    /// </summary>
    public override string ToString() =>
        $"{TeamName} ({TeamId}) - " +
        $"{RoleName} ({RoleId}) - " +
        $"Nodes: [{string.Join(", ", HierarchyNodes)}]";

    /// <summary>
    /// Um nó da hierarquia organizacional a que o vínculo dá acesso.
    ///
    /// <para>
    /// É um <c>record</c> de propósito, e não uma <c>class</c>: assim a igualdade é por valor, o
    /// que faz <see cref="AddHierarchyNode"/> conseguir detectar duplicata comparando conteúdo.
    /// </para>
    ///
    /// <para>
    /// As propriedades são <c>init</c>: definidas na criação e imutáveis depois. Para "mudar" um
    /// nó, cria-se outro.
    /// </para>
    /// </summary>
    public record HierarchyNode
    {
        /// <summary>Identificador do nó. É por ele que <see cref="RemoveHierarchyNode"/> remove.</summary>
        public string NodeId { get; init; } = null!;

        /// <summary>Nome do nó, para exibição.</summary>
        public string NodeName { get; init; } = null!;

        private HierarchyNode(string nodeId, string nodeName)
        {
            NodeId = nodeId;
            NodeName = nodeName;
        }

        /// <summary>Cria um nó. Construtor é privado para que esta seja a única porta.</summary>
        public static HierarchyNode Create(
            string nodeId,
            string nodeName)
            => new(nodeId, nodeName);
    }
}
