namespace Orchestrator.Domain;

/// <summary>
/// Quem criou a entidade e quando. Preenchido uma única vez, na construção, e nunca mais
/// alterado.
///
/// <para>
/// Todas as propriedades são <c>get</c> sem <c>set</c>, então nem a própria entidade consegue
/// reescrever a data de criação depois. É de propósito: "criado em" que pode ser editado não
/// serve de auditoria.
/// </para>
///
/// <para>
/// A classe é <c>sealed</c> porque não há caso para especializar auditoria — herdar daqui só
/// abriria margem para uma subclasse que registra diferente.
/// </para>
/// </summary>
public sealed class CreationInformation
{
    /// <param name="createdBy">
    /// Quem está criando. Vem do caso de uso, tipicamente do <c>CreatedBy</c> do request. Não é
    /// resolvido a partir do token aqui — o domínio não conhece HTTP.
    /// </param>
    public CreationInformation(string createdBy)
    {
        CreatedBy = createdBy;

        // UtcNow, nunca Now: o servidor pode rodar em outro fuso que a máquina de quem lê, e
        // gravar hora local torna impossível ordenar eventos de forma confiável. A conversão
        // para o fuso do usuário é problema de quem exibe.
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>Identificação de quem criou, como texto livre.</summary>
    public string CreatedBy { get; }

    /// <summary>Momento da criação, em UTC.</summary>
    public DateTime CreatedAt { get; }
}

/// <summary>
/// Quem alterou a entidade e quando — sempre a alteração <b>mais recente</b>.
///
/// <para>
/// Não é histórico: cada mutador substitui esta informação inteira, então só a última mudança
/// sobrevive. Se algum dia for preciso saber toda a sequência de alterações, isto não serve — o
/// caminho seria uma coleção de eventos, não mexer nesta classe.
/// </para>
///
/// <para>
/// Na entidade a propriedade correspondente é <b>anulável</b>: <c>null</c> significa "nunca foi
/// alterada desde que foi criada", que é um estado legítimo e não um dado faltando.
/// </para>
/// </summary>
public sealed class ModificationInformation
{
    /// <param name="modifiedBy">Quem está alterando, informado pelo caso de uso.</param>
    public ModificationInformation(string modifiedBy)
    {
        ModifiedBy = modifiedBy;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>Identificação de quem fez a última alteração.</summary>
    public string ModifiedBy { get; }

    /// <summary>Momento da última alteração, em UTC.</summary>
    public DateTime ModifiedAt { get; }
}
