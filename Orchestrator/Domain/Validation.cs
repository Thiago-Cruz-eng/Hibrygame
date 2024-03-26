namespace Orchestrator.Domain;

public class Validation
{
    public Guid Id { get; set; }
    public string AcessToken { get; set; }
    public string Room { get; set; }
    public string UserId { get; set; }
    public string PieceColor { get; set; }
    public string UserEmail { get; set; }
    public DateTime DayOfGame { get; set; } = DateTime.UtcNow;
}