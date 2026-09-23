using Orchestrator.UseCases;
using Xunit;

namespace Orchestrator.Test.UseCases;

/// <summary>
/// A limpeza de texto que vai para log (CWE-117, "log injection").
///
/// <para>
/// <b>O ataque que isto impede.</b> O log é lido como uma linha por evento — por pessoa e por
/// ferramenta de agregação. Um valor com <c>\n</c> passa a valer por várias linhas, e quem controla
/// esse valor passa a poder <b>forjar entradas de log</b>: uma sala chamada
/// <c>"sala\n2026-09-23 informação: usuário admin removido"</c> faz a investigação de um incidente
/// encontrar um evento que nunca aconteceu.
/// </para>
///
/// <para>
/// O template estruturado não protege disso sozinho: ele evita a concatenação, mas o valor continua
/// sendo escrito como texto no destino final.
/// </para>
/// </summary>
public class LogSanitizerTests
{
    // ---------------------------------------------------------------
    // Quebra de linha e controle
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("sala\nforjada")]
    [InlineData("sala\rforjada")]
    [InlineData("sala\r\nforjada")]
    public void Sanitize_RemovesLineBreaks(string value)
    {
        var sanitized = LogSanitizer.Sanitize(value);

        Assert.DoesNotContain('\n', sanitized);
        Assert.DoesNotContain('\r', sanitized);
    }

    [Fact]
    public void Sanitize_TurnsControlCharactersIntoSpaces_KeepingTheLength()
    {
        // Apagar juntaria palavras que estavam separadas e mudaria o valor mais do que o
        // necessário — quem lê o log tem de ver o que o usuário mandou, em uma linha só.
        Assert.Equal("sala forjada", LogSanitizer.Sanitize("sala\nforjada"));
    }

    [Theory]
    [InlineData("\t")]
    [InlineData("\v")]
    [InlineData("\0")]
    [InlineData("\u001b")]
    public void Sanitize_CoversEveryControlCharacter_NotJustLineBreaks(string control)
    {
        // Não é só `\n`: sequências de escape ANSI (`\u001b`) reescrevem o terminal de quem está
        // lendo o log, e o nulo trunca a linha em algumas ferramentas.
        Assert.Equal("a b", LogSanitizer.Sanitize($"a{control}b"));
    }

    [Fact]
    public void Sanitize_LeavesOrdinaryTextAlone()
    {
        // Acentuação e pontuação não são ameaça nenhuma e têm de sobreviver: o valor precisa
        // continuar reconhecível para quem depura.
        Assert.Equal("sala da tarde — nº 3", LogSanitizer.Sanitize("sala da tarde — nº 3"));
    }

    // ---------------------------------------------------------------
    // Ausência e tamanho
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sanitize_WithNothingToLog_SaysSo(string? value)
    {
        // Um campo em branco no meio de uma mensagem é indistinguível de erro de formatação.
        Assert.Equal("(vazio)", LogSanitizer.Sanitize(value));
    }

    [Fact]
    public void Sanitize_TruncatesAnOverlongValue()
    {
        // Valor longo não pode empurrar o resto da linha para fora.
        var sanitized = LogSanitizer.Sanitize(new string('x', 500));

        Assert.Equal(new string('x', 120) + "…", sanitized);
    }

    [Fact]
    public void Sanitize_AtTheLimit_DoesNotMarkTruncation()
    {
        // A fronteira: 120 passa inteiro, 121 ganha as reticências.
        Assert.Equal(new string('x', 120), LogSanitizer.Sanitize(new string('x', 120)));
        Assert.EndsWith("…", LogSanitizer.Sanitize(new string('x', 121)));
    }

    [Fact]
    public void Sanitize_TruncationDoesNotReintroduceControlCharacters()
    {
        // O corte acontece depois da limpeza, não antes.
        var sanitized = LogSanitizer.Sanitize(new string('x', 100) + "\n" + new string('y', 100));

        Assert.DoesNotContain('\n', sanitized);
    }
}
