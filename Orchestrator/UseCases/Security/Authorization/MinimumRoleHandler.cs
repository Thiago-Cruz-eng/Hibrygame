using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Orchestrator.UseCases.Security.Authorization;

/// <summary>
/// Decide se quem está chamando atende ao nível mínimo pedido por uma
/// <see cref="MinimumRoleRequirement"/>. É o que faz <c>[Authorize(Policy = "Role:Admin")]</c>
/// aceitar também <c>super adm</c>.
///
/// <para>
/// <b>Como o ASP.NET chega aqui</b> — vale entender, porque não há chamada explícita a esta
/// classe em lugar nenhum do código:
/// </para>
/// <list type="number">
///   <item><description>
///   <c>Program.cs</c> registra as policies (<c>"Role:Player"</c> … <c>"Role:SuperAdmin"</c>),
///   cada uma carregando uma <see cref="MinimumRoleRequirement"/> com o nível mínimo dela;
///   </description></item>
///   <item><description>
///   <c>Program.cs</c> registra este handler como <c>IAuthorizationHandler</c>;
///   </description></item>
///   <item><description>
///   ao encontrar <c>[Authorize(Policy = "...")]</c>, o framework procura os handlers capazes de
///   tratar aquele tipo de requisito e chama este método;
///   </description></item>
///   <item><description>
///   se nenhum handler chamar <c>Succeed</c>, a requisição é recusada — 403 para quem está
///   autenticado, 401 para quem não está.
///   </description></item>
/// </list>
///
/// <para>
/// <b>Por que não usar <c>[Authorize(Roles = "...")]</c>:</b> a forma nativa compara os papéis por
/// igualdade de texto, sem noção de ordem. <c>[Authorize(Roles = "adm")]</c> recusaria um
/// <c>super adm</c>, e cada atributo teria de enumerar todos os papéis acima do desejado. Por isso
/// o atributo cru é proibido no projeto: use sempre <c>Policy = "Role:X"</c>.
/// </para>
/// </summary>
public class MinimumRoleHandler : AuthorizationHandler<MinimumRoleRequirement>
{
    /// <summary>
    /// Aprova a requisição quando o maior papel presente no token alcança o mínimo exigido.
    ///
    /// <para>
    /// <b>Só aprova, nunca reprova.</b> O caminho negativo é simplesmente não chamar
    /// <c>Succeed</c> — o framework recusa por ausência de aprovação. Existe um <c>Fail()</c>, mas
    /// ele veta a requisição inteira mesmo que outro handler aprovasse, e não é isso que se quer
    /// aqui.
    /// </para>
    /// </summary>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MinimumRoleRequirement requirement)
    {
        var highestRole = FindHighestRole(context.User);

        // `null` cobre os dois casos de recusa por papel: token sem claim de papel e token cujos
        // papéis nenhum é reconhecido por RoleHierarchy.
        if (highestRole is not null && highestRole >= requirement.MinimumRole)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// O maior papel reconhecido entre os claims de <paramref name="user"/>, ou <c>null</c> se
    /// nenhum for reconhecido.
    ///
    /// <para>
    /// Um token pode trazer <b>vários</b> claims de papel — daí varrer todos e ficar com o maior,
    /// em vez de olhar só o primeiro. Papel não reconhecido é ignorado em silêncio: token antigo,
    /// ou emitido por outra versão, não deve derrubar a requisição com erro.
    /// </para>
    /// </summary>
    private static RoleLevel? FindHighestRole(ClaimsPrincipal user)
    {
        RoleLevel? highest = null;

        foreach (var claim in user.FindAll(ClaimTypes.Role))
        {
            if (!RoleHierarchy.TryGetLevel(claim.Value, out var level))
                continue;

            if (highest is null || level > highest)
            {
                highest = level;
            }
        }

        return highest;
    }
}
