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

    /// <summary>Nome da política de CORS. Referenciado de novo no pipeline, em <c>UseCors</c>.</summary>
    public const string CorsPolicyName = "AllowReactDevelopment";

    /// <summary>
    /// Registra controllers, Swagger, CORS e SignalR.
    /// </summary>
    public static IServiceCollection AddWebLayer(this IServiceCollection services)
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
                policy.WithOrigins(FrontendDevelopmentOrigin)
                    .AllowAnyHeader()
                    .AllowAnyMethod()

                    // Exigido pelo SignalR: sem AllowCredentials o handshake do hub é recusado pelo
                    // navegador. E é por isso que a origem tem de ser explícita — o navegador
                    // rejeita a combinação de credenciais com origem curinga.
                    .AllowCredentials();
            });
        });

        services.AddSignalR();

        return services;
    }
}
