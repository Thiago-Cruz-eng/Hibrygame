namespace Orchestrator.UseCases;

/// <summary>
/// Põe um e-mail na forma canônica — sem espaços nas pontas e em minúsculas — para que ele seja
/// gravado e comparado sempre igual.
///
/// <para>
/// <b>Por que isto existe como lugar único.</b> O e-mail é o login, e a busca por ele é uma
/// comparação exata de texto no MongoDB. Se um cadastro grava <c>"Ana@Teste.com "</c> e o login
/// procura <c>"ana@teste.com"</c>, os dois não casam e o usuário simplesmente não consegue entrar
/// — sem erro que explique o motivo. Normalizar nas duas pontas é o que impede isso, e para
/// funcionar as duas pontas têm de normalizar <b>do mesmo jeito</b>.
/// </para>
///
/// <para>
/// Até o refactor de 2026-08-03 esta função estava copiada, idêntica, dentro de
/// <c>CreateUserUseCase</c>, <c>LoginAsyncUseCase</c> e <c>UpdateUserUseCase</c>, mais uma
/// quarta cópia escrita à mão em <c>RegisterUserUseCase</c>. Quatro cópias de uma regra que só
/// funciona se todas concordarem é exatamente o caso em que uma delas fica para trás.
/// </para>
///
/// <para>
/// <b>Ao mexer aqui, lembre que existem dados já gravados.</b> Tornar a normalização mais
/// agressiva (remover pontos, ignorar sufixo <c>+tag</c>) faria o login deixar de encontrar
/// usuários cadastrados sob a regra antiga.
/// </para>
/// </summary>
public static class EmailNormalization
{
    /// <summary>
    /// Devolve <paramref name="value"/> sem espaços nas pontas e em minúsculas.
    /// </summary>
    /// <param name="value">
    /// O e-mail como veio do request. Aceita <c>null</c> porque o corpo de um request é
    /// desserializado de JSON e o campo pode simplesmente não ter vindo — mesmo quando o tipo em
    /// C# diz que não é anulável.
    /// </param>
    /// <returns>
    /// A forma canônica, ou string vazia quando <paramref name="value"/> é <c>null</c>. Vazio, e
    /// não exceção, para que a falta do campo vire "credencial inválida" no caso de uso, e não um
    /// erro 500.
    /// </returns>
    /// <remarks>
    /// <c>ToLowerInvariant</c>, e não <c>ToLower</c>: <c>ToLower</c> segue a cultura do sistema
    /// operacional, e há alfabetos em que a conversão de maiúsculas difere — o caso clássico é o
    /// turco, onde <c>'I'</c> vira <c>'ı'</c> e não <c>'i'</c>. Isso faria o mesmo e-mail ser
    /// normalizado de formas diferentes conforme o servidor, e o usuário conseguiria ou não entrar
    /// dependendo de onde a aplicação estivesse rodando.
    /// </remarks>
    public static string Normalize(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;
}
