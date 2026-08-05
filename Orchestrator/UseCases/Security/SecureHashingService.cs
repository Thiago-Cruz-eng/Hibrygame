using System.Security.Cryptography;
using Orchestrator.UseCases.Interfaces;

namespace Orchestrator.UseCases.Security;

/// <summary>
/// Transforma um segredo (senha ou refresh token) em algo que pode ser guardado no banco sem que
/// quem o leia consiga recuperar o original — e depois confere se um segredo apresentado
/// corresponde ao que foi guardado.
///
/// <para>
/// <b>Por que hash e não criptografia:</b> criptografia é reversível, e algo reversível guardado
/// junto da chave não protege ninguém. Hash é de mão única — nem o próprio sistema consegue
/// descobrir a senha de um usuário, e é por isso que "recuperar senha" nunca existe, só
/// "definir uma nova".
/// </para>
///
/// <para>
/// <b>PBKDF2, e por que 100.000 iterações:</b> um hash rápido é ruim aqui. Se testar um palpite
/// custa microssegundos, quem obtivesse o banco testaria bilhões de senhas por hora. PBKDF2
/// repete a derivação de propósito para tornar cada tentativa caro. O número é o botão de
/// custo: 100.000 é imperceptível num login (dezenas de milissegundos) e proibitivo em força
/// bruta.
/// </para>
///
/// <para>
/// <b>Não mexa nas constantes abaixo sem plano de migração.</b> Alterar iterações, tamanho de
/// chave ou algoritmo faz todo hash já gravado deixar de validar — na prática, todos os usuários
/// perdem a senha de uma vez. Migração se faz re-hasheando no próximo login bem-sucedido, com o
/// parâmetro antigo guardado por registro; nada disso existe hoje.
/// </para>
/// </summary>
public class SecureHashingService : ISecureHashingService
{
    /// <summary>
    /// Tamanho do salt em bytes (16 = 128 bits).
    ///
    /// <para>
    /// O salt é um valor aleatório, diferente por segredo, misturado antes de hashear. Ele resolve
    /// dois problemas: duas pessoas com a mesma senha passam a ter hashes diferentes (ninguém
    /// descobre senhas iguais comparando o banco), e tabelas de hashes pré-calculados deixam de
    /// servir, porque o atacante teria de refazer o trabalho para cada salt.
    /// </para>
    ///
    /// <para>
    /// O salt <b>não</b> é secreto — fica ao lado do hash no banco, e tem de ficar, porque a
    /// verificação precisa dele.
    /// </para>
    /// </summary>
    private const int SaltSize = 16;

    /// <summary>Tamanho do hash gerado, em bytes (32 = 256 bits, casando com SHA256).</summary>
    private const int KeySize = 32;

    /// <summary>Repetições da derivação. É o custo deliberado — ver a nota da classe.</summary>
    private const int Iterations = 100_000;

    /// <summary>
    /// Gera hash e salt para <paramref name="value"/>.
    ///
    /// <para>
    /// Chamar duas vezes com o mesmo valor devolve resultados <b>diferentes</b>, porque o salt é
    /// sorteado a cada chamada. Isso é o esperado: não compare hashes entre si para saber se dois
    /// segredos são iguais — use <see cref="Verify"/>.
    /// </para>
    /// </summary>
    /// <param name="value">O segredo em claro. Não é registrado em log em nenhum ponto.</param>
    /// <returns>
    /// Hash e salt, ambos em Base64 — texto, para caberem em campo <c>string</c> do MongoDB. Os
    /// dois precisam ser guardados; sem o salt o hash é inverificável.
    /// </returns>
    public (string Hash, string Salt) HashValue(string value)
    {
        // Gerador criptográfico, não Random: Random é previsível a partir da semente, e salt
        // previsível deixa de cumprir a função descrita em SaltSize.
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);

        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            value,
            saltBytes,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    /// <summary>
    /// Confere se <paramref name="value"/> é o segredo que gerou <paramref name="hash"/>.
    ///
    /// <para>
    /// A verificação é indireta, e é a única forma possível: re-deriva o hash de
    /// <paramref name="value"/> usando o <paramref name="salt"/> guardado e compara os dois
    /// hashes. O segredo original nunca é reconstruído.
    /// </para>
    /// </summary>
    /// <param name="value">O segredo apresentado por quem está tentando autenticar.</param>
    /// <param name="hash">Hash gravado, em Base64.</param>
    /// <param name="salt">Salt gravado junto, em Base64.</param>
    /// <returns><c>true</c> quando corresponde.</returns>
    public bool Verify(string value, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var hashBytes = Convert.FromBase64String(hash);

        // Mesmos parâmetros do HashValue — é isto que precisa continuar igual para os hashes
        // antigos seguirem validando.
        var computedHash = Rfc2898DeriveBytes.Pbkdf2(
            value,
            saltBytes,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        // FixedTimeEquals, e nunca `==` nem SequenceEqual: uma comparação normal para no
        // primeiro byte diferente, então o tempo de resposta revela QUANTOS bytes iniciais
        // estavam certos. Medindo isso repetidamente é possível descobrir o hash byte a byte.
        // Esta versão gasta o mesmo tempo sempre, independentemente de onde está a diferença.
        return CryptographicOperations.FixedTimeEquals(computedHash, hashBytes);
    }
}
