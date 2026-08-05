using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Orchestrator.Infra.Settings;

namespace Orchestrator.Composition;

/// <summary>
/// Autenticação: como um JWT que chega numa requisição é lido e validado.
///
/// <para>
/// Isto é o lado da <b>entrada</b>. Quem <b>emite</b> token é <c>TokenService</c>, e as duas pontas
/// têm de concordar em issuer, audience, algoritmo, chave e nome dos claims — divergência em
/// qualquer um faz todo token ser recusado.
/// </para>
///
/// <para>
/// <b>Sobre a pasta <c>Composition/</c>.</b> Estes arquivos existem só para dar nome aos blocos de
/// registro que antes eram 170 linhas corridas em <c>Program.cs</c>. Continua sendo registro
/// <b>manual</b>, um a um, sem varredura automática de assembly — isso é decisão deliberada do
/// projeto, e o que mudou foi apenas a legibilidade.
/// </para>
/// </summary>
public static class JwtComposition
{
    /// <summary>
    /// Tamanho mínimo da chave de assinatura, em bytes. HS256 exige 256 bits.
    /// </summary>
    private const int MinimumKeyBytes = 32;

    /// <summary>
    /// Lê a seção <c>"Jwt"</c> da configuração, disponibiliza-a por
    /// <c>IOptions&lt;JwtSettings&gt;</c> e confere que a chave serve.
    /// </summary>
    /// <returns>
    /// As configurações já materializadas, porque <c>Program.cs</c> precisa dos valores antes de a
    /// injeção de dependências existir.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Quando a seção não existe, ou quando a chave tem menos de <see cref="MinimumKeyBytes"/>
    /// bytes.
    ///
    /// <para>
    /// <b>Falhar na subida é intencional.</b> A chave que vinha no <c>appsettings.json</c> tinha
    /// 240 bits, e o efeito era desagradável de diagnosticar: a aplicação subia normalmente e TODO
    /// login falhava, porque <c>CreateAccessToken</c> estourava IDX10720 lá dentro e o caso de uso
    /// devolvia um "Login failed" genérico com 401 — indistinguível de senha errada. Melhor
    /// derrubar a subida dizendo o motivo.
    /// </para>
    /// </exception>
    public static JwtSettings AddJwtSettings(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("Jwt");

        services.Configure<JwtSettings>(section);

        var settings = section.Get<JwtSettings>()
            ?? throw new InvalidOperationException("Jwt settings are missing.");

        var keyBytes = Encoding.UTF8.GetByteCount(settings.Key ?? string.Empty);
        if (keyBytes < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:Key tem {keyBytes} bytes; HS256 exige pelo menos {MinimumKeyBytes} " +
                "(256 bits). Com uma chave menor nenhum token pode ser assinado e todo login " +
                "falha. Ajuste Jwt:Key na configuracao.");
        }

        return settings;
    }

    /// <summary>
    /// Configura a validação de JWT nas requisições.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, JwtSettings jwtSettings)
    {
        var signingKey = jwtSettings.Key ?? string.Empty;

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = true;

            // Guarda o token bruto no contexto da requisição. ValidationController depende disto:
            // ele precisa do token como o cliente o enviou, porque é assim que o registro de
            // validação foi gravado.
            options.SaveToken = true;

            // Sem isto o handler renomeia claims na entrada — `sub` vira
            // ClaimTypes.NameIdentifier, `email` vira ClaimTypes.Email — e todo
            // `User.FindFirst(JwtRegisteredClaimNames.Sub)` devolve null.
            //
            // Consequencia real: ValidationController.IsCallerAuthorizedFor compara o `sub` com o
            // userId do corpo, entao os QUATRO endpoints de /validation respondiam 403 para
            // qualquer usuario, sempre. Como o lobby chama verifyValidation antes de entrar numa
            // sala, era impossivel entrar em sala pela interface.
            //
            // TokenService emite `sub` e ClaimTypes.Role; desligar o mapeamento faz os nomes no
            // servidor serem exatamente os que estao no token.
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateLifetime = true,
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),

                // Sem tolerância de relógio. O padrão do .NET é 5 minutos, o que faria um token
                // expirado continuar sendo aceito por esse tempo.
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = ReadTokenFromQueryStringForHub
            };
        });

        return services;
    }

    /// <summary>
    /// Permite que a conexão com o <c>/chesshub</c> traga o token em <c>?access_token=</c>.
    ///
    /// <para>
    /// <b>Por que existe esta exceção à regra de "token nunca em URL".</b> O handshake do WebSocket
    /// é feito pelo navegador e não aceita cabeçalho <c>Authorization</c> personalizado — é
    /// limitação do protocolo, não escolha. O cliente SignalR então manda o token na query string, e
    /// isto o recupera.
    /// </para>
    ///
    /// <para>
    /// <b>A checagem de caminho é o que limita o estrago.</b> Sem o <c>StartsWithSegments</c>,
    /// qualquer endpoint REST passaria a aceitar token por URL — e URL vaza para log de servidor,
    /// histórico de navegador e cabeçalho <c>Referer</c>. Não amplie este filtro.
    /// </para>
    /// </summary>
    private static Task ReadTokenFromQueryStringForHub(MessageReceivedContext context)
    {
        var accessToken = context.Request.Query["access_token"];
        var path = context.HttpContext.Request.Path;

        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chesshub"))
        {
            context.Token = accessToken;
        }

        return Task.CompletedTask;
    }
}
