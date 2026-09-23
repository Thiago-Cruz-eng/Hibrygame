using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Orchestrator.Composition;
using Xunit;

namespace Orchestrator.Test.Composition;

/// <summary>
/// O middleware de cabeçalhos de segurança.
///
/// <para>
/// <b>O que ele faz é instruir o navegador</b>, não o servidor: nenhum cabeçalho aqui muda o corpo
/// de resposta nenhuma. Eles fecham caminhos de abuso que dependem de o navegador ser permissivo —
/// interpretar um JSON como HTML, embutir a API num <c>iframe</c> alheio, vazar a URL no
/// <c>Referer</c>, guardar um token no cache de disco.
/// </para>
///
/// <para>
/// <b>Por que o teste precisa de um dublê de <see cref="IHttpResponseFeature"/>.</b> O middleware
/// grava os cabeçalhos dentro de <c>Response.OnStarting</c>, para que valham no último instante
/// possível — já com o status e o caminho finais. Só que a implementação padrão de
/// <c>HttpResponseFeature</c>, a que o <see cref="DefaultHttpContext"/> usa, <b>descarta</b> esses
/// callbacks: ela não tem servidor por trás para iniciar resposta nenhuma. Sem o dublê abaixo, todo
/// teste deste arquivo passaria sem verificar nada, porque nenhum cabeçalho chegaria a ser escrito.
/// </para>
/// </summary>
public class SecurityHeadersTests
{
    // ---------------------------------------------------------------
    // Infraestrutura do teste
    // ---------------------------------------------------------------

    /// <summary>
    /// Um <see cref="IHttpResponseFeature"/> que <b>guarda</b> os callbacks de
    /// <c>OnStarting</c> e permite dispará-los à mão, imitando o que um servidor real faz ao
    /// começar a enviar a resposta.
    /// </summary>
    private sealed class RecordingResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStarting = [];

        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;

        /// <summary>Sempre <c>false</c>: é o que faz o <c>OnStarting</c> ser aceito.</summary>
        public bool HasStarted => false;

        public void OnStarting(Func<object, Task> callback, object state)
            => _onStarting.Add((callback, state));

        /// <summary>Não exercitado por estes testes.</summary>
        public void OnCompleted(Func<object, Task> callback, object state) { }

        /// <summary>Dispara os callbacks na ordem em que foram registrados.</summary>
        public async Task StartResponseAsync()
        {
            foreach (var (callback, state) in _onStarting)
            {
                await callback(state);
            }
        }
    }

    /// <summary>
    /// Passa uma requisição pelo middleware e devolve os cabeçalhos da resposta já iniciada.
    /// </summary>
    /// <param name="path">Caminho da requisição — é o que decide CSP e <c>Cache-Control</c>.</param>
    /// <param name="terminal">
    /// O que o resto do pipeline faz. O padrão é um 200 vazio; os testes passam algo diferente para
    /// simular resposta de erro ou cabeçalho já definido por outro middleware.
    /// </param>
    private static async Task<IHeaderDictionary> HeadersFor(
        string path,
        RequestDelegate? terminal = null)
    {
        var responseFeature = new RecordingResponseFeature();

        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(new HttpRequestFeature { Path = path });
        features.Set<IHttpResponseFeature>(responseFeature);

        var context = new DefaultHttpContext(features);

        var builder = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
        builder.UseSecurityHeaders();
        builder.Run(terminal ?? (RequestDelegate)(_ => Task.CompletedTask));

        await builder.Build()(context);

        // O servidor real faria isto ao começar a enviar a resposta.
        await responseFeature.StartResponseAsync();

        return responseFeature.Headers;
    }

    // ---------------------------------------------------------------
    // Os cabeçalhos que valem em toda resposta
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("Referrer-Policy", "no-referrer")]
    [InlineData("Permissions-Policy", "camera=(), microphone=(), geolocation=()")]
    public async Task EveryResponse_CarriesTheHeader(string name, string expected)
    {
        var headers = await HeadersFor("/users/abc");

        Assert.Equal(expected, headers[name]);
    }

    [Fact]
    public async Task EveryResponse_CarriesTheApiContentSecurityPolicy()
    {
        // Esta API não serve documento nenhum: nada pode ser carregado, e nada pode embuti-la.
        var headers = await HeadersFor("/users/abc");

        Assert.Equal("default-src 'none'; frame-ancestors 'none'", headers["Content-Security-Policy"]);
    }

    [Fact]
    public async Task AnErrorResponse_CarriesTheHeadersToo()
    {
        // Cabeçalho que só aparece no caminho feliz não protege nada: 401, 403 e 429 são
        // respostas como quaisquer outras, e é por isso que o middleware entra antes da
        // autenticação e escreve em OnStarting.
        var headers = await HeadersFor("/users/abc", context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        });

        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", headers["X-Frame-Options"]);
    }

    [Fact]
    public async Task AHeaderSetEarlierIsReplaced_NotDuplicated()
    {
        // Atribuição, não Append: dois valores no mesmo cabeçalho de segurança é pior que um,
        // porque o navegador escolhe por conta própria qual respeitar.
        var headers = await HeadersFor("/users/abc", context =>
        {
            context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
            return Task.CompletedTask;
        });

        Assert.Equal("DENY", headers["X-Frame-Options"]);
        Assert.Single(headers["X-Frame-Options"].ToArray());
    }

    // ---------------------------------------------------------------
    // CSP: a exceção do Swagger
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/v1/swagger.json")]
    public async Task SwaggerPaths_DoNotGetTheContentSecurityPolicy(string path)
    {
        // O Swagger UI é HTML de verdade, com script e estilo próprios: `default-src 'none'` o
        // deixaria em branco. Ele só existe em Development — ver Program.cs.
        var headers = await HeadersFor(path);

        Assert.False(headers.ContainsKey("Content-Security-Policy"));
    }

    [Fact]
    public async Task SwaggerPaths_StillGetEveryOtherHeader()
    {
        // A exceção é da CSP e só dela. Servir HTML não é motivo para abrir mão do resto.
        var headers = await HeadersFor("/swagger/index.html");

        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", headers["X-Frame-Options"]);
        Assert.Equal("no-referrer", headers["Referrer-Policy"]);
    }

    [Fact]
    public async Task APathThatMerelyStartsWithTheWordSwagger_StillGetsTheContentSecurityPolicy()
    {
        // StartsWithSegments compara SEGMENTOS: "/swaggerfake" não está sob "/swagger", e deixar
        // que estivesse seria uma forma barata de escapar da política.
        var headers = await HeadersFor("/swaggerfake");

        Assert.Equal("default-src 'none'; frame-ancestors 'none'", headers["Content-Security-Policy"]);
    }

    // ---------------------------------------------------------------
    // Cache-Control: só onde a resposta carrega credencial
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("/login")]
    [InlineData("/register")]
    [InlineData("/refresh-token")]
    public async Task CredentialPaths_AreNotStored(string path)
    {
        // As três devolvem access token e refresh token no corpo. Sem no-store, um proxy ou o
        // cache de disco do navegador guarda essa resposta, e ela sobrevive ao logout.
        var headers = await HeadersFor(path);

        Assert.Equal("no-store", headers.CacheControl);
    }

    [Theory]
    [InlineData("/users/abc")]
    [InlineData("/validation/verify")]
    [InlineData("/chesshub")]
    [InlineData("/loginfake")]
    public async Task OtherPaths_DoNotGetNoStore(string path)
    {
        // Marcar tudo como no-store custaria banda e latência em resposta que não carrega segredo.
        var headers = await HeadersFor(path);

        Assert.True(StringValues.IsNullOrEmpty(headers.CacheControl));
    }

    [Fact]
    public async Task CredentialPathsAreMatchedCaseInsensitively()
    {
        // O roteamento do ASP.NET não diferencia maiúscula de minúscula; a proteção não pode
        // diferenciar, ou basta chamar /LOGIN para a resposta voltar a ser cacheável.
        var headers = await HeadersFor("/LOGIN");

        Assert.Equal("no-store", headers.CacheControl);
    }
}
