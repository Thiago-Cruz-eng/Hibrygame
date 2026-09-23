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
