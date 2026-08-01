namespace Orchestrator.UseCases.Dto.Response;

public class VerifyValidationResponse
{
    public bool Valid { get; set; }
}

public class GetValidationResponse
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string UserEmail { get; set; } = null!;
    public string? Room { get; set; }
    public string? PieceColor { get; set; }
    public DateTime DayOfGame { get; set; }
}

public class UpdateValidationResponse
{
    public bool Updated { get; set; }
}

public class CanMoveValidationResponse
{
    public bool CanMove { get; set; }
}
