namespace Orchestrator.UseCases;

/// <summary>
/// Limpa texto vindo do usuário antes de ele entrar num registro de log.
///
/// <para>
/// <b>O problema que isto resolve</b> é conhecido como <i>log injection</i> (CWE-117). O log é
/// lido como uma linha por evento — por pessoa e por ferramenta de agregação. Um valor que
/// carregue <c>\r</c> ou <c>\n</c> passa a valer por várias linhas, e quem controla esse valor
/// passa a poder <b>forjar entradas de log</b>: basta enviar uma sala chamada
/// <c>"sala\n2026-09-23 informação: usuário admin removido"</c> para que a investigação de um
/// incidente encontre um evento que nunca aconteceu.
/// </para>
///
/// <para>
/// O template estruturado (<c>"... sala {Room}"</c>) não protege disso sozinho: ele evita a
/// concatenação, mas o valor continua sendo escrito como texto no destino final.
/// </para>
///
/// <para>
/// <b>Quando aplicar.</b> Todo parâmetro de log que seja <c>string</c> e tenha atravessado a
/// fronteira HTTP — corpo de request, rota, query, <b>ou claim de token</b>. Claim é menos
/// arriscado (o token é assinado por este servidor), mas a análise estática não distingue um
/// do outro, e nem deveria: se um dia o claim passar a carregar texto livre, a proteção já
/// está no lugar.
/// </para>
///
/// <para>
/// <b>Quando NÃO aplicar.</b> Valor que não é texto livre — <see cref="Guid"/>, <c>int</c>,
/// <c>enum</c>, constante do próprio código. Um <c>Guid</c> não tem como carregar um
/// <c>\n</c>, e sanitizá-lo só acrescenta ruído a quem lê o código.
/// </para>
/// </summary>
public static class LogSanitizer
{
    /// <summary>Teto de tamanho: valor longo não deve empurrar o resto da linha para fora.</summary>
    private const int MaxLength = 120;

    /// <summary>
    /// <paramref name="value"/> sem quebras de linha nem caracteres de controle, truncado.
    /// </summary>
    /// <returns>
    /// <c>"(vazio)"</c> quando não há nada a registrar — um campo em branco no meio de uma
    /// mensagem é indistinguível de um erro de formatação.
    /// </returns>
    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "(vazio)";

        var buffer = new char[Math.Min(value.Length, MaxLength)];
        var length = 0;

        foreach (var character in value)
        {
            if (length == buffer.Length) break;

            // Controle vira espaço em vez de sumir: apagar caracteres juntaria palavras que
            // estavam separadas e mudaria o valor mais do que o necessário.
            buffer[length++] = char.IsControl(character) ? ' ' : character;
        }

        var sanitized = new string(buffer, 0, length);
        return value.Length > MaxLength ? sanitized + "…" : sanitized;
    }
}
