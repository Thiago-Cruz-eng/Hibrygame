using Orchestrator.Infra.Utils;

namespace Orchestrator.Domain;

/// <summary>
/// Registro de que um usuário está autorizado a jogar uma cor numa sala.
///
/// <para>
/// <b>Esta entidade NÃO segue o contrato descrito em <see cref="BaseEntity"/>, e você não deve
/// copiá-la como molde.</b> Use <see cref="User"/> para isso. As diferenças, todas herdadas:
/// </para>
/// <list type="bullet">
///   <item><description>
///   setters <c>public</c> — qualquer código consegue reescrever qualquer campo, inclusive o
///   token e a cor, sem passar por regra nenhuma;
///   </description></item>
///   <item><description>
///   sem <c>Create(...)</c> nem construtor privado: é montada com inicializador de objeto
///   (<c>new Validation { ... }</c>), então não existe ponto único onde validar o que é
///   obrigatório;
///   </description></item>
///   <item><description>
///   sem auditoria — não há como saber quem criou ou alterou o registro.
///   </description></item>
/// </list>
///
/// <para>
/// <b>Superfície de segurança (DT-07 e DT-20).</b> Duas coisas a saber antes de mexer:
/// </para>
/// <list type="bullet">
///   <item><description>
///   <see cref="AcessToken"/> guardava o access token <b>em claro</b>, e quem lesse a coleção
///   saía com sessões prontas para usar. Desde o endurecimento de 2026-09-23 o campo guarda um
///   resumo SHA-256 (<c>TokenDigest</c>), gravado e comparado por <c>ValidationService</c>;
///   </description></item>
///   <item><description>
///   <c>ValidationService.GetValidationCanMove</c> recebe e-mail e dia e <b>ignora os dois</b>
///   no filtro. Ampliar o que esta entidade autoriza sem antes decidir o filtro correto
///   aumenta o alcance de um problema que já existe.
///   </description></item>
/// </list>
///
/// <para>
/// A decisão pendente é se este subdomínio deve continuar existindo — o hub SignalR já
/// revalida identidade, turno, posse e legalidade por conta própria, o que torna esta
/// autorização paralela redundante. Ver <c>docs/debito-tecnico.md</c> e a skill
/// <c>validacao-de-sessao-de-jogo</c>.
/// </para>
/// </summary>
[CollectionName(nameof(Validation))]
public class Validation : BaseEntity
{
    /// <summary>
    /// <b>Resumo SHA-256</b> do access token do usuário — nunca o token em si.
    ///
    /// <para>
    /// O resumo é determinístico e sem salt, de propósito: o filtro do Mongo compara por
    /// igualdade, então o mesmo token precisa produzir sempre o mesmo texto. Quem calcula é
    /// <c>TokenDigest</c>, e é lá que está a explicação de por que SHA-256 basta para um segredo
    /// de alta entropia.
    /// </para>
    ///
    /// <para>
    /// O nome tem um erro de digitação — deveria ser <c>AccessToken</c>, com dois "c". Está
    /// mantido porque o nome da propriedade é também o nome do campo no MongoDB: renomear
    /// deixaria os documentos já gravados inacessíveis, e o projeto não tem script de migração.
    /// Corrigir exige migração de dados, não só renome.
    /// </para>
    /// </summary>
    public string AcessToken { get; set; } = null!;

    /// <summary>Sala a que a autorização se refere. <c>null</c> antes de o usuário entrar numa.</summary>
    public string? Room { get; set; }

    /// <summary>
    /// Dono da autorização, como texto. É <c>string</c> e não <see cref="Guid"/> porque o valor
    /// chega assim do token e é comparado assim no filtro.
    /// </summary>
    public string UserId { get; set; } = null!;

    /// <summary>Cor que o usuário pode mover: <c>"White"</c> ou <c>"Black"</c>.</summary>
    public string? PieceColor { get; set; }

    /// <summary>E-mail do usuário. Gravado, mas nenhum filtro o usa — ver DT-20.</summary>
    public string UserEmail { get; set; } = null!;

    /// <summary>
    /// Dia da partida, em UTC. Também gravado e também não usado em filtro nenhum: a
    /// autorização não expira por data.
    /// </summary>
    public DateTime DayOfGame { get; set; } = DateTime.UtcNow;
}
