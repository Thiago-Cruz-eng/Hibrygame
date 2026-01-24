namespace Orchestrator.UseCases.Security.Authorization;

public enum RoleLevel
{
    Player = 1,
    MainPlayer = 2,
    TeamLeader = 3,
    Admin = 4,
    SuperAdmin = 5
}

public static class RoleHierarchy
{
    private static readonly Dictionary<string, RoleLevel> RoleMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jogador"] = RoleLevel.Player,
        ["jogador principal"] = RoleLevel.MainPlayer,
        ["lider de time"] = RoleLevel.TeamLeader,
        ["adm"] = RoleLevel.Admin,
        ["super adm"] = RoleLevel.SuperAdmin
    };

    public static bool TryGetLevel(string role, out RoleLevel level)
        => RoleMap.TryGetValue(role.Trim(), out level);

    public static string NormalizeRole(RoleLevel level) => level switch
    {
        RoleLevel.Player => "jogador",
        RoleLevel.MainPlayer => "jogador principal",
        RoleLevel.TeamLeader => "lider de time",
        RoleLevel.Admin => "adm",
        RoleLevel.SuperAdmin => "super adm",
        _ => "jogador"
    };
}
