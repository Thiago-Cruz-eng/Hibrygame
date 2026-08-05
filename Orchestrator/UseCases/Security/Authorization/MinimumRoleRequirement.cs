using Microsoft.AspNetCore.Authorization;

namespace Orchestrator.UseCases.Security.Authorization;

/// <summary>
/// O parâmetro de uma policy: "para passar, é preciso ser pelo menos isto".
///
/// <para>
/// Classe sem comportamento de propósito — ela só transporta o nível mínimo. Quem decide é
/// <see cref="MinimumRoleHandler"/>. Essa separação entre "o que se exige" (requisito) e "como se
/// verifica" (handler) é do próprio ASP.NET Core: o mesmo requisito pode ser atendido por mais de
/// um handler, por exemplo se um dia o papel puder vir de outra fonte além do claim do JWT.
/// </para>
///
/// <para>
/// Uma instância por policy, criada no <c>Program.cs</c>: cinco policies, cinco requisitos, cada
/// um com o seu nível.
/// </para>
/// </summary>
public class MinimumRoleRequirement : IAuthorizationRequirement
{
    /// <param name="minimumRole">
    /// Nível mínimo aceito. Quem tiver este nível <b>ou qualquer um acima</b> passa.
    /// </param>
    public MinimumRoleRequirement(RoleLevel minimumRole)
    {
        MinimumRole = minimumRole;
    }

    /// <summary>Nível mínimo exigido. Somente leitura: o requisito não muda depois de criado.</summary>
    public RoleLevel MinimumRole { get; }
}
