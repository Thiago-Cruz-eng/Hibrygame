using MongoDB.Bson.Serialization.Attributes;

namespace Orchestrator.Domain;

/// <summary>
/// Raiz de toda entidade persistida. Dá a cada uma a única coisa que todas precisam: uma
/// identidade.
///
/// <para>
/// <b>O contrato de entidade deste projeto</b> — o que se espera de qualquer classe que herde
/// daqui. Vale a pena ler antes de criar a primeira:
/// </para>
/// <list type="number">
///   <item><description>
///   <b>Setters <c>protected</c> ou <c>private</c>.</b> Nada de <c>public set</c>: quem está
///   fora da entidade não muda o estado dela na mão. Isso impede que metade do sistema altere
///   um usuário sem passar pelas regras — e sem registrar auditoria.
///   </description></item>
///   <item><description>
///   <b>Um construtor <c>protected</c> vazio.</b> Não é para você usar; é para o driver do
///   MongoDB, que precisa instanciar a classe antes de preencher os campos ao ler do banco.
///   Sem ele a leitura falha em tempo de execução.
///   </description></item>
///   <item><description>
///   <b>Construtor real <c>private</c> + <c>static Create(...)</c>.</b> O método de fábrica é a
///   única porta de entrada para criar a entidade. Ganha-se um nome onde documentar o que é
///   obrigatório e um lugar único para validar.
///   </description></item>
///   <item><description>
///   <b>Mutador nomeado, devolvendo <c>this</c>.</b> Em vez de <c>user.Name = x</c>, escreve-se
///   <c>user.ChangeName(x, quemMudou)</c>. O nome diz a intenção, e devolver <c>this</c>
///   permite encadear.
///   </description></item>
///   <item><description>
///   <b>Auditoria.</b> <see cref="CreationInformation"/> preenchido na criação e
///   <see cref="ModificationInformation"/> em <b>cada</b> mutador. Mutador que esquece disso
///   deixa a trilha de auditoria mentindo.
///   </description></item>
///   <item><description>
///   <b><c>[CollectionName(nameof(Entidade))]</c> na classe</b>, para fixar o nome da coleção
///   no MongoDB.
///   </description></item>
/// </list>
///
/// <para>
/// <see cref="User"/> é o exemplo que segue o contrato inteiro — use-o como molde.
/// <see cref="Validation"/> é a exceção que não segue, e está anotada dizendo por quê.
/// </para>
/// </summary>
public class BaseEntity
{
    /// <summary>
    /// Identidade da entidade. Já vem preenchida: um <see cref="Guid"/> novo é gerado na
    /// construção, então uma entidade nunca existe sem id, nem antes de ir ao banco.
    ///
    /// <para>
    /// Gerar o id na aplicação, e não deixar o banco gerar, é o que permite ao caso de uso
    /// devolver o id ao cliente na mesma operação em que salva.
    /// </para>
    ///
    /// <para>
    /// <b>Como isso vira dado no MongoDB:</b> <c>[BsonId]</c> marca a propriedade como a chave
    /// primária (o campo <c>_id</c> do documento). <c>[BsonIgnoreIfDefault]</c> omite o campo
    /// quando o valor é <c>Guid.Empty</c>, deixando o Mongo gerar um id nesse caso. E o
    /// <c>Guid</c> é gravado como <b>string</b>, não como binário — isso é decidido
    /// globalmente no <c>Program.cs</c>, com <c>GuidSerializer(BsonType.String)</c>, para que os
    /// ids fiquem legíveis ao inspecionar a coleção à mão.
    /// </para>
    /// </summary>
    [BsonId]
    [BsonIgnoreIfDefault]
    public Guid Id { get; set; } = Guid.NewGuid();
}
