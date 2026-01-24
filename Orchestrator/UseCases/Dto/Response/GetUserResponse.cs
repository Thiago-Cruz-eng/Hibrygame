using Orchestrator.UseCases.Dto;

namespace Orchestrator.UseCases.Dto.Response;

public class GetUserResponse
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Role { get; set; } = null!;
    public bool MustChangePassword { get; set; }
    public List<UserAssignmentDto> Assignments { get; set; } = new();
}
