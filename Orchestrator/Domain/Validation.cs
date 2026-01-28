using Orchestrator.Infra.Mongo;
using Orchestrator.Infra.Utils;

namespace Orchestrator.Domain;

[CollectionName(nameof(Validation))]
public class Validation : BaseEntity
{
    public string AcessToken { get; set; }
    public string? Room { get; set; }
    public string UserId { get; set; }
    public string? PieceColor { get; set; }
    public string UserEmail { get; set; }
    public DateTime DayOfGame { get; set; } = DateTime.UtcNow;
}
