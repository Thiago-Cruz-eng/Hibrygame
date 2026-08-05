using Microsoft.AspNetCore.Authorization;
using Orchestrator.UseCases.Security.Authorization;

namespace Orchestrator.Composition;

/// <summary>
/// Autorização: as cinco policies de papel e o handler que as decide.
/// </summary>
public static class AuthorizationComposition
{
    /// <summary>
    /// Registra uma policy por papel, cada uma exigindo aquele nível <b>ou superior</b>.
    ///
    /// <para>
    /// <b>Use sempre <c>[Authorize(Policy = "Role:X")]</c></b>, nunca
    /// <c>[Authorize(Roles = "...")]</c>. O atributo nativo compara papéis por igualdade de texto e
    /// não conhece hierarquia: <c>Roles = "adm"</c> recusaria um <c>super adm</c>. Quem entende a
    /// ordem é <see cref="MinimumRoleHandler"/>, e ele só entra em cena via policy.
    /// </para>
    ///
    /// <para>
    /// <b>Ao acrescentar um papel</b> em <see cref="RoleLevel"/>, acrescente a policy aqui também —
    /// as duas listas precisam andar juntas, e nada no compilador garante isso.
    /// </para>
    /// </summary>
    public static IServiceCollection AddRolePolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Role:Player", policy =>
                policy.Requirements.Add(new MinimumRoleRequirement(RoleLevel.Player)));
            options.AddPolicy("Role:MainPlayer", policy =>
                policy.Requirements.Add(new MinimumRoleRequirement(RoleLevel.MainPlayer)));
            options.AddPolicy("Role:TeamLeader", policy =>
                policy.Requirements.Add(new MinimumRoleRequirement(RoleLevel.TeamLeader)));
            options.AddPolicy("Role:Admin", policy =>
                policy.Requirements.Add(new MinimumRoleRequirement(RoleLevel.Admin)));
            options.AddPolicy("Role:SuperAdmin", policy =>
                policy.Requirements.Add(new MinimumRoleRequirement(RoleLevel.SuperAdmin)));
        });

        // Singleton porque o handler não guarda estado nem depende de nada por requisição. Sem este
        // registro as policies acima existem mas nada as avalia, e toda requisição autorizada é
        // recusada.
        services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();

        return services;
    }
}
