using System.Text.Json.Serialization;

namespace Orchestrator.Composition;

/// <summary>
/// A camada web: controllers, serialização JSON, CORS, Swagger e SignalR.
/// </summary>
public static class WebComposition
{
    /// <summary>
    /// Origem do frontend em desenvolvimento. O KrockSide roda aqui (<c>vite preview</c> usa a
    /// porta 3000).
    /// </summary>
    private const string FrontendDevelopmentOrigin = "http://localhost:3000";

    /// <summary>Chave de configuração com as origens permitidas. Array de strings.</summary>
    private const string AllowedOriginsKey = "Cors:AllowedOrigins";

    /// <summary>
    /// Teto do corpo de uma mensagem do hub, em bytes (32 KB).
    ///
    /// <para>
    /// As invocações do <c>/chesshub</c> carregam nome de sala e duas casas em notação algébrica —
    /// dezenas de bytes. O padrão do SignalR é 32 KB e é mantido explicitamente aqui para que o
    /// valor seja uma decisão visível, e não um padrão herdado que ninguém sabe qual é.
    /// </para>
    /// </summary>
    private const long MaximumHubMessageBytes = 32 * 1024;

    /// <summary>Nome da política de CORS. Referenciado de novo no pipeline, em <c>UseCors</c>.</summary>
    public const string CorsPolicyName = "AllowReactDevelopment";

    /// <summary>
    /// Registra controllers, Swagger, CORS e SignalR.
    /// </summary>
    /// <param name="configuration">
    /// Fonte das origens de CORS (<c>Cors:AllowedOrigins</c>). Ausente, vale só
    /// <c>http://localhost:3000</c> — o frontend em desenvolvimento. <b>Em produção a lista tem de
    /// ser definida</b>, ou o navegador do usuário real recusa toda chamada.
    /// </param>
    public static IServiceCollection AddWebLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers().AddJsonOptions(options =>
            // ReferenceHandler.Preserve resolve ciclos de referência ao serializar, mas tem um
            // custo visível: o JSON ganha marcadores `$id` e `$ref`, e uma lista vira
            // `{ "$id": "1", "$values": [...] }`. O frontend precisa saber lidar com isso — está
            // registrado em docs/FRONTEND_CHANGES.md. Não remova sem alinhar as duas pontas.
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                policy.WithOrigins(ResolveAllowedOrigins(configuration))
                    .AllowAnyHeader()
                    .AllowAnyMethod()

                    // Exigido pelo SignalR: sem AllowCredentials o handshake do hub é recusado pelo
                    // navegador. E é por isso que a origem tem de ser explícita — o navegador
                    // rejeita a combinação de credenciais com origem curinga.
                    .AllowCredentials();
            });
        });

        services.AddSignalR(options =>
        {
            // Teto explícito de tamanho de mensagem — ver MaximumHubMessageBytes.
            options.MaximumReceiveMessageSize = MaximumHubMessageBytes;

            // Detalhe de exceção não volta para o cliente. Com isto ligado, um erro dentro de um
            // método do hub devolve a mensagem e a pilha da exceção ao navegador, o que descreve
            // as entranhas do servidor para quem está sondando. O motivo real fica no log.
            options.EnableDetailedErrors = false;
        });

        return services;
    }

    /// <summary>
    /// As origens que o navegador pode usar para chamar esta API.
    /// </summary>
    /// <returns>
    /// O que estiver em <c>Cors:AllowedOrigins</c>; se a chave não existir ou vier vazia, apenas
    /// a origem de desenvolvimento. Nunca devolve lista vazia: <c>WithOrigins</c> sem nenhuma
    /// origem recusaria tudo, e um erro de configuração viraria "o sistema parou" sem pista.
    /// </returns>
    private static string[] ResolveAllowedOrigins(IConfiguration configuration)
    {
        var configured = configuration.GetSection(AllowedOriginsKey)
            .Get<string[]>()?
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .ToArray();

        return configured is { Length: > 0 } ? configured : [FrontendDevelopmentOrigin];
    }
}
