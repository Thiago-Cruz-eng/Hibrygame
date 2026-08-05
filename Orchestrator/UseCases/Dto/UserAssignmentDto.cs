using Orchestrator.Domain;

namespace Orchestrator.UseCases.Dto;

/// <summary>
/// Um vínculo de usuário como ele chega e sai pela API — a forma de transporte de
/// <see cref="UserAssignment"/>.
///
/// <para>
/// <b>Por que existe um DTO separado da entidade.</b> A entidade tem setters protegidos e é criada
/// por <c>Create(...)</c>, justamente para que ninguém a monte pela metade; o desserializador de
/// JSON precisa exatamente do contrário — construtor vazio e setters públicos. Além disso, expor a
/// entidade na API amarraria o contrato HTTP ao domínio: qualquer renome interno viraria uma
/// mudança incompatível para o frontend.
/// </para>
/// </summary>
public class UserAssignmentDto
{
    /// <summary>Nome do time.</summary>
    public string TeamName { get; set; } = null!;

    /// <summary>Identificador do time.</summary>
    public string TeamId { get; set; } = null!;

    /// <summary>
    /// Nome do papel dentro do time. Não confundir com o papel de autorização do usuário — ver
    /// <see cref="UserAssignment.RoleName"/>.
    /// </summary>
    public string RoleName { get; set; } = null!;

    /// <summary>Identificador do papel.</summary>
    public string RoleId { get; set; } = null!;

    /// <summary>Nós de hierarquia deste vínculo. Lista vazia é válido.</summary>
    public List<HierarchyNodeDto> HierarchyNodes { get; set; } = new();

    /// <summary>
    /// Converte este DTO na entidade de domínio correspondente, com os nós aninhados.
    ///
    /// <para>
    /// Mora aqui, junto do DTO, e não dentro dos casos de uso, porque estava duplicado: uma cópia
    /// idêntica em <c>CreateUserUseCase</c> e outra em <c>UpdateUserUseCase</c>, diferindo apenas
    /// no nome do parâmetro. Sendo o DTO quem conhece a própria forma, é dele a
    /// responsabilidade de saber traduzir-se.
    /// </para>
    /// </summary>
    /// <param name="changedBy">
    /// Quem está criando ou alterando. <b>Hoje não tem efeito nenhum</b>:
    /// <see cref="UserAssignment"/> recebe o valor e o descarta, por não ter campo de auditoria.
    /// Está repassado para que o dia em que a auditoria for acrescentada lá não exija mexer aqui.
    /// </param>
    public UserAssignment ToDomain(string changedBy)
    {
        var nodes = HierarchyNodes
            .Select(node => UserAssignment.HierarchyNode.Create(node.NodeId, node.NodeName))
            .ToList();

        return UserAssignment.Create(
            TeamName,
            TeamId,
            RoleName,
            RoleId,
            nodes,
            changedBy);
    }

    /// <summary>
    /// Caminho inverso de <see cref="ToDomain"/>: converte a entidade no DTO, para sair pela API.
    ///
    /// <para>
    /// Estava dentro de <c>GetUserUseCase</c>. Ficou aqui junto do <see cref="ToDomain"/> porque as
    /// duas conversões são a mesma correspondência de campos lida em direções opostas — separá-las
    /// é o que faz uma ganhar um campo novo e a outra esquecer.
    /// </para>
    /// </summary>
    public static UserAssignmentDto FromDomain(UserAssignment assignment) => new()
    {
        TeamName = assignment.TeamName,
        TeamId = assignment.TeamId,
        RoleName = assignment.RoleName,
        RoleId = assignment.RoleId,
        HierarchyNodes = assignment.HierarchyNodes
            .Select(node => new HierarchyNodeDto
            {
                NodeId = node.NodeId,
                NodeName = node.NodeName
            })
            .ToList()
    };
}

/// <summary>
/// Um nó de hierarquia como ele trafega pela API. Espelha
/// <see cref="UserAssignment.HierarchyNode"/>, que no domínio é um <c>record</c> imutável.
/// </summary>
public class HierarchyNodeDto
{
    /// <summary>Identificador do nó.</summary>
    public string NodeId { get; set; } = null!;

    /// <summary>Nome do nó, para exibição.</summary>
    public string NodeName { get; set; } = null!;
}
