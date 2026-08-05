namespace Orchestrator.UseCases.Interfaces;

/// <summary>
/// Guarda segredos de forma irreversível e confere se um segredo apresentado corresponde ao
/// guardado. Usado para senha de usuário e para refresh token.
///
/// <para>
/// Existe como interface para que os casos de uso não dependam da implementação de criptografia:
/// trocar o algoritmo, ou substituir por um dublê no teste, não toca em nenhum caso de uso. A
/// implementação é <c>SecureHashingService</c> (PBKDF2), e é lá que estão as notas sobre por que
/// os parâmetros são o que são.
/// </para>
/// </summary>
public interface ISecureHashingService
{
    /// <summary>
    /// Gera hash e salt de <paramref name="value"/>.
    ///
    /// <para>
    /// Chamadas repetidas com o mesmo valor devolvem resultados diferentes, porque o salt é novo a
    /// cada vez. Portanto <b>não compare hashes</b> para saber se dois segredos são iguais — use
    /// <see cref="Verify"/>.
    /// </para>
    /// </summary>
    /// <returns>
    /// O hash e o salt, ambos em texto. <b>Guarde os dois</b>: sem o salt o hash é inverificável.
    /// </returns>
    (string Hash, string Salt) HashValue(string value);

    /// <summary>
    /// Confere se <paramref name="value"/> é o segredo que originou <paramref name="hash"/>.
    /// </summary>
    /// <param name="value">O segredo apresentado.</param>
    /// <param name="hash">Hash gravado.</param>
    /// <param name="salt">Salt gravado junto do hash.</param>
    bool Verify(string value, string hash, string salt);
}
