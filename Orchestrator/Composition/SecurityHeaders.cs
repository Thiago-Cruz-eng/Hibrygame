namespace Orchestrator.Composition;

/// <summary>
/// Cabeçalhos de segurança acrescentados a toda resposta.
///
/// <para>
/// São instruções para o <b>navegador</b>, não para a API. Nenhum deles muda o que o servidor
/// faz: eles fecham caminhos de abuso que dependem de o navegador ser permissivo — interpretar um
/// JSON como HTML, embutir a API num <c>iframe</c> alheio, vazar a URL no <c>Referer</c>, guardar
/// um token no cache do disco.
/// </para>
///
/// <para>
/// <b>Ordem no pipeline.</b> Entra cedo, antes de autenticação e de <c>MapControllers</c>, para
/// valer também nas respostas que nunca chegam a um controller — 401, 403, 429 e a página de
/// erro. Um cabeçalho que só aparece no caminho feliz não protege nada.
/// </para>
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// Rotas cuja resposta carrega credencial e portanto não pode ser guardada em cache.
    ///
    /// <para>
    /// As três devolvem access token e refresh token no corpo. Sem <c>no-store</c>, um proxy ou o
    /// cache de disco do navegador pode guardar essa resposta, e ela sobrevive ao logout.
    /// </para>
    /// </summary>
    private static readonly string[] CredentialPaths = ["/login", "/refresh-token", "/register"];

    /// <summary>
    /// Política de conteúdo para uma API que não serve HTML: nada pode ser carregado, e nada pode
    /// embutir esta origem.
    /// </summary>
    private const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    /// <summary>
    /// Acrescenta os cabeçalhos a cada resposta.
    ///
    /// <para>
    /// <b>Por que <c>OnStarting</c> e não escrever direto:</b> o middleware roda antes do resto do
    /// pipeline, e quem vier depois ainda pode substituir cabeçalhos. Registrando no callback de
    /// início da resposta, os valores são gravados no último instante possível, já com o status
    /// e o caminho finais conhecidos.
    /// </para>
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;

                // Impede o navegador de "adivinhar" o tipo do conteúdo. Sem isto, uma resposta
                // JSON com texto controlado pelo usuário pode ser tratada como HTML e executar
                // script na origem da API.
                headers["X-Content-Type-Options"] = "nosniff";

                // Nenhuma página pode embutir esta origem em frame. Vale para navegador antigo
                // que não entende frame-ancestors.
                headers["X-Frame-Options"] = "DENY";

                // A URL desta API nunca viaja como Referer para terceiros — e URLs de API
                // carregam identificadores.
                headers["Referrer-Policy"] = "no-referrer";

                // Desliga recursos de dispositivo para qualquer documento servido daqui.
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

                // O Swagger UI é HTML de verdade, com script e estilo próprios: a política
                // restrita o deixaria em branco. Fora dele, a API não serve documento nenhum.
                if (!context.Request.Path.StartsWithSegments("/swagger"))
                {
                    headers["Content-Security-Policy"] = ApiContentSecurityPolicy;
                }

                if (IsCredentialPath(context.Request.Path))
                {
                    headers.CacheControl = "no-store";
                }

                return Task.CompletedTask;
            });

            await next();
        });

    /// <summary>A requisição está num dos caminhos que devolvem credencial?</summary>
    private static bool IsCredentialPath(PathString path) =>
        CredentialPaths.Any(credentialPath =>
            path.StartsWithSegments(credentialPath, StringComparison.OrdinalIgnoreCase));
}
