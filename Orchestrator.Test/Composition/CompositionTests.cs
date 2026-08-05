using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Orchestrator.Composition;
using Orchestrator.Infra.BaseRepository;
using Orchestrator.Infra.Interfaces;
using Orchestrator.Infra.Mongo;
using Orchestrator.UseCases;
using Orchestrator.UseCases.Interfaces;
using Orchestrator.UseCases.Security.Authorization;
using Xunit;

namespace Orchestrator.Test.Composition;

/// <summary>
/// Testes da composição de DI de <c>Orchestrator/Composition/</c>.
///
/// <para>
/// <b>Por que isto merece teste.</b> O registro de serviço é a única parte do projeto onde um erro
/// <b>compila</b> e só aparece em tempo de execução — esquecer de registrar um caso de uso novo passa
/// pelo compilador e falha quando a primeira requisição chega ao controller. Resolver cada
/// registro aqui transforma esse erro de runtime em erro de suíte.
/// </para>
///
/// <para>
/// Nenhum destes testes precisa de MongoDB. Construir um <see cref="MongoClient"/> e chamar
/// <c>GetDatabase</c> não abre conexão — o driver conecta na primeira operação, e nenhuma acontece
/// aqui.
/// </para>
/// </summary>
public class CompositionTests
{
    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    /// <summary>Uma chave de 32 bytes, o mínimo que HS256 aceita.</summary>
    private const string ValidKey = "0123456789abcdef0123456789abcdef";

    private static IConfiguration BuildConfiguration(string? key = ValidKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = key,
                ["Jwt:Issuer"] = "https://localhost:5001",
                ["Jwt:Audience"] = "https://localhost:5001",
                ["Jwt:ExpiresMinutes"] = "60",
                ["Jwt:RefreshTokenDays"] = "30",
                ["Mongo:ConnectionString"] = "mongodb://localhost:27017",
                ["Mongo:Database"] = "HibrygameTest",
            })
            .Build();

    /// <summary>
    /// A aplicação inteira composta, como o <c>Program.cs</c> a monta — menos o servidor web.
    /// </summary>
    private static ServiceProvider BuildFullProvider()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration();

        // ILogger é fornecido pelo host em produção; aqui entra à mão, porque todo caso de uso
        // depende de um.
        services.AddLogging();

        var jwtSettings = services.AddJwtSettings(configuration);
        services.AddJwtAuthentication(jwtSettings);
        services.AddRolePolicies();
        services.AddWebLayer();
        services.AddMongoPersistence(configuration);
        services.AddUseCases();

        return services.BuildServiceProvider();
    }

    // ---------------------------------------------------------------
    // AddJwtSettings — a guarda de tamanho de chave
    // ---------------------------------------------------------------

    [Fact]
    public void AddJwtSettings_ValidKey_ReturnsSettingsFromConfiguration()
    {
        var services = new ServiceCollection();

        var settings = services.AddJwtSettings(BuildConfiguration());

        Assert.Equal(ValidKey, settings.Key);
        Assert.Equal("https://localhost:5001", settings.Issuer);
        Assert.Equal(60, settings.ExpiresMinutes);
        Assert.Equal(30, settings.RefreshTokenDays);
    }

    [Fact]
    public void AddJwtSettings_ValidKey_RegistersOptionsForInjection()
    {
        var services = new ServiceCollection();
        services.AddJwtSettings(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<Orchestrator.Infra.Settings.JwtSettings>>();

        Assert.Equal(ValidKey, options.Value.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("curta")]
    // 31 bytes: um a menos que o mínimo. É a fronteira que importa.
    [InlineData("0123456789abcdef0123456789abcde")]
    public void AddJwtSettings_KeyShorterThan32Bytes_Throws(string shortKey)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddJwtSettings(BuildConfiguration(shortKey)));

        // A mensagem tem de dizer o motivo: era exatamente a falta disso que fazia todo login
        // falhar com "Login failed" genérico, indistinguível de senha errada.
        Assert.Contains("Jwt:Key", exception.Message);
        Assert.Contains("32", exception.Message);
    }

    [Fact]
    public void AddJwtSettings_MissingSection_Throws()
    {
        var services = new ServiceCollection();
        var empty = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddJwtSettings(empty));

        Assert.Contains("Jwt settings are missing", exception.Message);
    }

    [Fact]
    public void AddJwtSettings_KeyOfExactly32Bytes_DoesNotThrow()
    {
        var services = new ServiceCollection();

        var settings = services.AddJwtSettings(BuildConfiguration(ValidKey));

        Assert.Equal(32, System.Text.Encoding.UTF8.GetByteCount(settings.Key));
    }

    // ---------------------------------------------------------------
    // AddJwtAuthentication — como o token que chega é validado
    // ---------------------------------------------------------------

    /// <summary>
    /// As opções do esquema JwtBearer, já materializadas.
    ///
    /// <para>
    /// Resolver por <see cref="IOptionsMonitor{T}"/> é o que faz o lambda de configuração de
    /// <c>AddJwtBearer</c> executar — ele é preguiçoso, e sem isto nada dentro dele roda.
    /// </para>
    /// </summary>
    private static JwtBearerOptions ResolveJwtBearerOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var jwtSettings = services.AddJwtSettings(BuildConfiguration());
        services.AddJwtAuthentication(jwtSettings);

        var provider = services.BuildServiceProvider();
        return provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
    }

    [Fact]
    public void AddJwtAuthentication_DisablesInboundClaimMapping()
    {
        var options = ResolveJwtBearerOptions();

        // A checagem mais importante deste arquivo. Com o mapeamento ligado, `sub` vira
        // ClaimTypes.NameIdentifier e todo FindFirst(JwtRegisteredClaimNames.Sub) devolve null — o
        // que fazia os QUATRO endpoints de /validation responderem 403 para qualquer usuário,
        // sempre, e tornava impossível entrar em sala pela interface.
        Assert.False(options.MapInboundClaims);
    }

    [Fact]
    public void AddJwtAuthentication_SavesTokenSoValidationControllerCanReadIt()
    {
        var options = ResolveJwtBearerOptions();

        // ValidationController precisa do token bruto, como o cliente o enviou, porque é assim que
        // o registro de validação foi gravado.
        Assert.True(options.SaveToken);
    }

    [Fact]
    public void AddJwtAuthentication_ValidatesEverythingAndAllowsNoClockSkew()
    {
        var options = ResolveJwtBearerOptions();
        var parameters = options.TokenValidationParameters;

        Assert.True(parameters.ValidateLifetime);
        Assert.True(parameters.ValidateAudience);
        Assert.True(parameters.ValidateIssuer);
        Assert.True(parameters.ValidateIssuerSigningKey);
        Assert.Equal("https://localhost:5001", parameters.ValidIssuer);
        Assert.Equal("https://localhost:5001", parameters.ValidAudience);

        // O padrão do .NET é 5 minutos de tolerância, o que manteria um token expirado válido por
        // esse tempo.
        Assert.Equal(TimeSpan.Zero, parameters.ClockSkew);
    }

    [Fact]
    public void AddJwtAuthentication_RequiresHttpsMetadata()
    {
        Assert.True(ResolveJwtBearerOptions().RequireHttpsMetadata);
    }

    // --- O token na query string: a única exceção, e o filtro que a limita ---

    /// <summary>
    /// Roda o handler de <c>OnMessageReceived</c> contra um caminho e uma query string.
    /// </summary>
    /// <returns>O token que o handler aceitou, ou <c>null</c> se ele não aceitou nenhum.</returns>
    private static async Task<string?> ReadTokenFor(string path, string? accessToken)
    {
        var options = ResolveJwtBearerOptions();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        if (accessToken is not null)
        {
            httpContext.Request.QueryString = new QueryString($"?access_token={accessToken}");
        }

        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            displayName: null,
            handlerType: typeof(JwtBearerHandler));

        var context = new MessageReceivedContext(httpContext, scheme, options);

        await options.Events!.OnMessageReceived(context);

        return context.Token;
    }

    [Fact]
    public async Task OnMessageReceived_ChessHubPathWithAccessToken_AcceptsIt()
    {
        // WebSocket não carrega cabeçalho Authorization próprio — é limitação do protocolo, e é a
        // razão de esta exceção existir.
        Assert.Equal("abc123", await ReadTokenFor("/chesshub", "abc123"));
    }

    [Fact]
    public async Task OnMessageReceived_ChessHubSubPathWithAccessToken_AcceptsIt()
    {
        // O SignalR acrescenta segmentos ao negociar (/chesshub/negotiate).
        Assert.Equal("abc123", await ReadTokenFor("/chesshub/negotiate", "abc123"));
    }

    [Theory]
    [InlineData("/login")]
    [InlineData("/users")]
    [InlineData("/validation/verify")]
    [InlineData("/chesshubfake")]
    public async Task OnMessageReceived_RestPathWithAccessToken_IgnoresIt(string path)
    {
        // É o que limita o estrago: sem o filtro de caminho, QUALQUER endpoint REST passaria a
        // aceitar token por URL — e URL vaza para log de servidor, histórico de navegador e
        // cabeçalho Referer.
        Assert.Null(await ReadTokenFor(path, "abc123"));
    }

    [Fact]
    public async Task OnMessageReceived_ChessHubPathWithoutAccessToken_LeavesTokenUnset()
    {
        Assert.Null(await ReadTokenFor("/chesshub", accessToken: null));
    }

    [Fact]
    public async Task OnMessageReceived_ChessHubPathWithEmptyAccessToken_LeavesTokenUnset()
    {
        Assert.Null(await ReadTokenFor("/chesshub", string.Empty));
    }

    // ---------------------------------------------------------------
    // AddRolePolicies
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("Role:Player", RoleLevel.Player)]
    [InlineData("Role:MainPlayer", RoleLevel.MainPlayer)]
    [InlineData("Role:TeamLeader", RoleLevel.TeamLeader)]
    [InlineData("Role:Admin", RoleLevel.Admin)]
    [InlineData("Role:SuperAdmin", RoleLevel.SuperAdmin)]
    public void AddRolePolicies_EachPolicy_CarriesItsMinimumRole(string policyName, RoleLevel expected)
    {
        var services = new ServiceCollection();
        services.AddRolePolicies();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthorizationOptions>>();

        var policy = options.Value.GetPolicy(policyName);

        Assert.NotNull(policy);
        var requirement = Assert.Single(policy!.Requirements.OfType<MinimumRoleRequirement>());
        Assert.Equal(expected, requirement.MinimumRole);
    }

    [Fact]
    public void AddRolePolicies_RegistersMinimumRoleHandler()
    {
        var services = new ServiceCollection();
        services.AddRolePolicies();

        using var provider = services.BuildServiceProvider();

        // Sem este registro as policies existem mas nada as avalia, e toda requisição autorizada
        // seria recusada.
        var handlers = provider.GetServices<IAuthorizationHandler>();
        Assert.Contains(handlers, h => h is MinimumRoleHandler);
    }

    // ---------------------------------------------------------------
    // AddWebLayer
    // ---------------------------------------------------------------

    [Fact]
    public void AddWebLayer_CorsPolicy_AllowsTheFrontendOriginWithCredentials()
    {
        var services = new ServiceCollection();
        services.AddWebLayer();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<CorsOptions>>();

        var policy = options.Value.GetPolicy(WebComposition.CorsPolicyName);

        Assert.NotNull(policy);
        Assert.Contains("http://localhost:3000", policy!.Origins);

        // AllowCredentials é exigido pelo handshake do SignalR, e é por isso que a origem tem de
        // ser explícita — o navegador recusa credenciais com origem curinga.
        Assert.True(policy.SupportsCredentials);
        Assert.True(policy.AllowAnyHeader);
        Assert.True(policy.AllowAnyMethod);
    }

    // ---------------------------------------------------------------
    // AddMongoPersistence
    // ---------------------------------------------------------------

    [Fact]
    public void AddMongoPersistence_ResolvesContextFromConfiguredDatabase()
    {
        var services = new ServiceCollection();
        services.AddMongoPersistence(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<IMongoDbContext>();

        Assert.Equal("HibrygameTest", context.Database.DatabaseNamespace.DatabaseName);
    }

    [Fact]
    public void AddMongoPersistence_WithoutConfiguration_FallsBackToLocalDefaults()
    {
        var services = new ServiceCollection();
        services.AddMongoPersistence(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<IMongoDbContext>();

        // É o fallback que vale hoje na prática: o appsettings.json declara a seção como
        // HibrygameDatabase:*, que nunca casa com Mongo:*. Ver DT-06.
        Assert.Equal("Hibrygame", context.Database.DatabaseNamespace.DatabaseName);
    }

    [Fact]
    public void AddMongoPersistence_MongoClient_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddMongoPersistence(BuildConfiguration());

        using var provider = services.BuildServiceProvider();

        // O cliente administra um pool de conexões e é feito para viver o processo inteiro. Um por
        // requisição esgotaria conexões no banco.
        Assert.Same(
            provider.GetRequiredService<IMongoClient>(),
            provider.GetRequiredService<IMongoClient>());
    }

    [Theory]
    [InlineData(typeof(IGenericRepository))]
    [InlineData(typeof(IUserRepositoryNoSql))]
    [InlineData(typeof(IRefreshTokenRepositoryNoSql))]
    [InlineData(typeof(IValidationRepositoryNoSql))]
    public void AddMongoPersistence_EveryRepository_Resolves(Type contract)
    {
        var services = new ServiceCollection();
        services.AddMongoPersistence(BuildConfiguration());

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService(contract));
    }

    // ---------------------------------------------------------------
    // AddUseCases — o registro que é fácil esquecer
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(typeof(CreateUserUseCase))]
    [InlineData(typeof(GetUserUseCase))]
    [InlineData(typeof(LoginAsyncUseCase))]
    [InlineData(typeof(UpdateUserUseCase))]
    [InlineData(typeof(DeleteUserUseCase))]
    [InlineData(typeof(ChangePasswordUseCase))]
    [InlineData(typeof(RefreshTokenUseCase))]
    [InlineData(typeof(RegisterUserUseCase))]
    public void AddUseCases_EveryUseCase_ResolvesWithItsDependencies(Type useCase)
    {
        using var provider = BuildFullProvider();

        // Resolver de verdade, e não só conferir que o descriptor existe: é o que também prova que
        // as dependências transitivas de cada caso de uso estão registradas.
        Assert.NotNull(provider.GetRequiredService(useCase));
    }

    [Theory]
    [InlineData(typeof(ISecureHashingService))]
    [InlineData(typeof(ITokenService))]
    [InlineData(typeof(IValidationService))]
    public void AddUseCases_EveryApplicationService_Resolves(Type contract)
    {
        using var provider = BuildFullProvider();

        Assert.NotNull(provider.GetRequiredService(contract));
    }

    [Fact]
    public void AddUseCases_RegisterUserUseCase_ComposesCreateUserAndLogin()
    {
        using var provider = BuildFullProvider();

        // RegisterUserUseCase depende dos outros dois em vez de repetir a emissão de token. Se um
        // deles saísse do registro, isto quebraria antes de a aplicação subir.
        Assert.NotNull(provider.GetRequiredService<RegisterUserUseCase>());
        Assert.NotNull(provider.GetRequiredService<CreateUserUseCase>());
        Assert.NotNull(provider.GetRequiredService<LoginAsyncUseCase>());
    }

    [Fact]
    public void FullComposition_TokenService_ReceivesTheConfiguredJwtSettings()
    {
        using var provider = BuildFullProvider();

        // Prova que a ponte entre AddJwtSettings e quem consome IOptions<JwtSettings> está de pé —
        // era o caminho que ficava quebrado quando a chave era curta.
        var tokenService = provider.GetRequiredService<ITokenService>();

        Assert.IsType<Orchestrator.UseCases.Security.TokenService>(tokenService);
    }
}
