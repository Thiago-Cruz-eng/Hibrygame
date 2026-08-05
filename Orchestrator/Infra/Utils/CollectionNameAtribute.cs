namespace Orchestrator.Infra.Utils;

/// <summary>
/// Declara em qual coleção do MongoDB uma entidade é gravada.
///
/// <para>
/// Uso, sempre com <c>nameof</c> para que renomear a classe não passe despercebido:
/// </para>
/// <code>
/// [CollectionName(nameof(User))]
/// public class User : BaseEntity { }
/// </code>
///
/// <para>
/// Quem lê este atributo é <c>GenericRepository.GetCollection&lt;T&gt;()</c>. Sem o atributo o
/// repositório cai no nome do tipo em C#, o que funciona mas deixa o nome da coleção — e
/// portanto os dados já gravados — dependente do nome da classe. Por isso o projeto exige o
/// atributo em toda entidade.
/// </para>
///
/// <para>
/// <b>Nota de arquitetura (DT-02):</b> este atributo mora em <c>Infra/Utils</c> e é aplicado nas
/// classes de <c>Domain/</c>, o que faz o domínio importar de infraestrutura e inverte o fluxo de
/// dependência declarado na constituição. É desvio conhecido e catalogado — não amplie o uso
/// fora das entidades.
/// </para>
///
/// <para>
/// <b>Nota:</b> o nome do arquivo (<c>CollectionNameAtribute.cs</c>) tem um erro de digitação e
/// não bate com o da classe (<c>CollectionNameAttribute</c>). Está mantido para não gerar renome
/// de arquivo sem necessidade; a classe é que vale.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public class CollectionNameAttribute : Attribute
{
    /// <summary>Nome da coleção no MongoDB.</summary>
    public string CollectionName { get; }

    public CollectionNameAttribute(string name)
    {
        CollectionName = name;
    }
}
