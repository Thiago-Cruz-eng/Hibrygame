using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Orchestrator.Composition;

/// <summary>
/// Limite de requisições por origem — a barreira contra força bruta de senha e contra enxurrada
/// de chamadas.
///
/// <para>
/// <b>Por que isto existe.</b> O login confere a senha com PBKDF2 de 100.000 iterações, o que
/// torna cada tentativa cara <b>para o servidor</b> — e não impede ninguém de tentar um milhão de
/// vezes. Sem teto, um atacante percorre uma lista de senhas comuns contra cada e-mail conhecido,
/// e de quebra derruba a API pelo custo do próprio hash.
/// </para>
///
/// <para>
/// <b>Dois tetos, com papéis diferentes:</b> uma política nomeada
/// (<see cref="AuthPolicyName"/>), estreita, aplicada só onde se apresenta credencial; e um
/// limitador global, largo, que protege o resto da API. Endpoint de autenticação novo
/// <b>precisa</b> declarar <c>[EnableRateLimiting(RateLimitingComposition.AuthPolicyName)]</c> —
/// nada o faz automaticamente.
/// </para>
///
/// <para>
/// <b>Particionado por IP remoto.</b> É a única identidade disponível antes de a autenticação
/// acontecer — e é o que se quer limitar: quem ainda não provou ser ninguém. Atrás de proxy, o
/// <c>RemoteIpAddress</c> é o do proxy; em produção isso exige
/// <c>ForwardedHeaders</c> configurado, ou o teto passa a valer para a soma de todos os
/// clientes. Está registrado em <c>docs/seguranca.md</c>.
/// </para>
///
/// <para>
/// Nada de pacote novo: <c>Microsoft.AspNetCore.RateLimiting</c> vem no framework compartilhado
/// desde o .NET 7.
/// </para>
/// </summary>
public static class RateLimitingComposition
{
    /// <summary>
    /// Nome da política estreita. Referenciado nos atributos do <c>UserController</c> — os dois
    /// lados têm de concordar, e errar o nome resulta em <b>nenhum</b> limite, em silêncio.
    /// </summary>
    public const string AuthPolicyName = "auth";

    /// <summary>Teto por minuto nos endpoints de credencial, quando a configuração não diz outro.</summary>
    public const int DefaultAuthPermitPerMinute = 20;

    /// <summary>Teto por minuto no restante da API, quando a configuração não diz outro.</summary>
    public const int DefaultGlobalPermitPerMinute = 300;

    /// <summary>Chave usada quando o IP não está disponível (teste, socket unix, proxy mal configurado).</summary>
    private const string UnknownClientKey = "unknown";

    /// <summary>
    /// Corpo devolvido no 429. JSON com o mesmo formato de <c>{ success, message }</c> que o
    /// frontend já trata nos outros erros — 429 não deve exigir um caminho de leitura próprio.
    /// </summary>
    private static readonly byte[] RejectionBody = JsonSerializer.SerializeToUtf8Bytes(
        new { success = false, message = "Too many requests" });

    /// <summary>
    /// Registra o limitador global e a política <see cref="AuthPolicyName"/>.
    /// </summary>
    /// <param name="configuration">
    /// Lê <c>RateLimiting:AuthPermitPerMinute</c> e <c>RateLimiting:GlobalPermitPerMinute</c>.
    /// Valor ausente ou inválido cai no padrão — configuração errada não pode desligar a
    /// proteção sem ninguém perceber.
    /// </param>
    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authPermit = ReadPermit(configuration, "RateLimiting:AuthPermitPerMinute", DefaultAuthPermitPerMinute);
        var globalPermit = ReadPermit(configuration, "RateLimiting:GlobalPermitPerMinute", DefaultGlobalPermitPerMinute);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(AuthPolicyName, context =>
                FixedWindowByClient(ResolveClientKey(context), authPermit));

            // GlobalLimiter roda em TODA requisição, inclusive nas que também passam pela
            // política estreita — os dois tetos se somam, e o menor é o que vale.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                FixedWindowByClient($"global:{ResolveClientKey(context)}", globalPermit));

            options.OnRejected = WriteRejectionAsync;
        });

        return services;
    }

    /// <summary>
    /// A janela fixa usada pelas duas políticas: <paramref name="permitLimit"/> requisições por
    /// minuto, sem fila.
    ///
    /// <para>
    /// <c>QueueLimit = 0</c> de propósito: enfileirar transformaria o excesso em espera, e espera
    /// é exatamente o recurso que um ataque de volume quer consumir. Recusar na hora é mais
    /// barato para o servidor e mais honesto para o cliente.
    /// </para>
    /// </summary>
    public static RateLimitPartition<string> FixedWindowByClient(string partitionKey, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });

    /// <summary>
    /// A identidade usada para particionar: o IP remoto.
    /// </summary>
    /// <returns>
    /// <c>"unknown"</c> quando não há IP. Todos os clientes sem IP dividem a mesma partição —
    /// é conservador de propósito: prefere limitar demais a não limitar nada.
    /// </returns>
    public static string ResolveClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? UnknownClientKey;

    /// <summary>
    /// Escreve o 429 com <c>Retry-After</c>, quando o limitador sabe dizer quanto falta.
    /// </summary>
    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.ContentType = "application/json";

        // Só a janela fixa sabe informar o tempo restante; com outro algoritmo o metadado não
        // vem e o cabeçalho é omitido em vez de chutado.
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await response.Body.WriteAsync(RejectionBody, cancellationToken);
    }

    /// <summary>
    /// Lê um teto da configuração, caindo no padrão quando ausente, não numérico ou não positivo.
    /// </summary>
    private static int ReadPermit(IConfiguration configuration, string key, int fallback)
    {
        var raw = configuration[key];
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : fallback;
    }
}
