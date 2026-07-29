namespace Tracker.Domain.Common;

public abstract class EntidadeBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
