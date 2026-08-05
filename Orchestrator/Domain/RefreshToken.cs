using Orchestrator.Infra.Utils;

namespace Orchestrator.Domain;

/// <summary>
/// Um refresh token emitido para um usuário: o crédito que permite obter um access token novo
/// sem pedir a senha outra vez.
///
/// <para>
/// <b>O valor do token não está aqui.</b> O que se guarda é <see cref="TokenHash"/>, o hash com
/// salt do valor sorteado. O valor em claro existe uma única vez, na resposta HTTP que o entrega
/// ao cliente, e depois disso ninguém — nem quem tem acesso ao banco — consegue reconstruí-lo.
/// Por isso a verificação funciona ao contrário do intuitivo: recebe-se o token do cliente,
/// re-deriva-se o hash com o salt guardado e comparam-se os hashes.
/// </para>
///
/// <para>
/// <b>Rotação.</b> Cada uso gasta o token: o antigo é revogado e um novo é emitido, com
/// <see cref="ReplacedByTokenId"/> apontando para o sucessor. A corrente que se forma permite
/// detectar reutilização — se alguém apresentar um token já revogado, é sinal de que o valor
/// vazou, e a corrente inteira pode ser invalidada.
/// </para>
/// </summary>
[CollectionName(nameof(RefreshToken))]
public class RefreshToken : BaseEntity
{
    /// <summary>
    /// Dono do token. <c>init</c> em vez de <c>private set</c>: definido na construção e nunca
    /// depois — um token não troca de usuário.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>Hash do valor sorteado. Nunca o valor em claro. Ver a nota da classe.</summary>
    public string TokenHash { get; private set; } = null!;

    /// <summary>
    /// Salt usado para gerar <see cref="TokenHash"/>. Único por token, e é o que impede que dois
    /// tokens iguais produzam o mesmo hash — e que um atacante pré-compute hashes.
    /// </summary>
    public string Salt { get; private set; } = null!;

    /// <summary>Quando o token deixa de valer. Em UTC.</summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>Quando foi emitido. Em UTC.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Quando foi revogado, ou <c>null</c> se ainda não foi. Revogação é explícita — expirar
    /// pelo tempo <b>não</b> preenche este campo.
    /// </summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>
    /// Token que substituiu este na rotação. É o elo da corrente descrita na nota da classe.
    /// <c>null</c> quando a revogação não gerou sucessor (logout, por exemplo).
    /// </summary>
    public Guid? ReplacedByTokenId { get; private set; }

    /// <summary>Motivo da revogação, para diagnóstico.</summary>
    public string? ReasonRevoked { get; private set; }

    /// <summary>Passou da validade? Cuidado: expirado não é o mesmo que revogado.</summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    /// Ainda serve para renovar? Exige as duas condições: não revogado <b>e</b> não expirado.
    /// É esta propriedade que o caso de uso de refresh deve consultar, não as duas separadas.
    /// </summary>
    public bool IsActive => RevokedAt is null && !IsExpired;

    /// <summary>Construtor para o MongoDB desserializar. Não use — chame <see cref="Create"/>.</summary>
    protected RefreshToken() { }

    private RefreshToken(Guid userId, string tokenHash, string salt, DateTime expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        Salt = salt;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Emite um token para <paramref name="userId"/>.
    /// </summary>
    /// <param name="tokenHash">
    /// Hash do valor sorteado — <b>já hasheado</b>. Quem sorteia o valor e o hasheia é
    /// <c>TokenService</c>; esta entidade nunca vê o valor em claro e por isso não tem como
    /// impedir que alguém passe um valor cru aqui por engano.
    /// </param>
    /// <param name="salt">Salt usado nesse hash.</param>
    /// <param name="expiresAt">Validade, em UTC.</param>
    public static RefreshToken Create(Guid userId, string tokenHash, string salt, DateTime expiresAt)
        => new(userId, tokenHash, salt, expiresAt);

    /// <summary>
    /// Invalida o token.
    ///
    /// <para>
    /// <b>Idempotente:</b> chamar de novo num token já revogado não faz nada — em particular, não
    /// sobrescreve o <see cref="RevokedAt"/> nem o motivo originais. Isso importa porque a
    /// primeira revogação é a que conta para investigar um vazamento; uma segunda chamada
    /// apagaria a evidência.
    /// </para>
    /// </summary>
    /// <param name="reason">Por que está sendo revogado.</param>
    /// <param name="replacedByTokenId">
    /// O sucessor, quando a revogação faz parte de uma rotação. Deixe <c>null</c> quando não há
    /// sucessor.
    /// </param>
    public void Revoke(string reason, Guid? replacedByTokenId = null)
    {
        if (RevokedAt is not null)
            return;

        RevokedAt = DateTime.UtcNow;
        ReasonRevoked = reason;
        ReplacedByTokenId = replacedByTokenId;
    }
}
