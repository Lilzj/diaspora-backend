namespace Diaspora.Contracts.Dtos;

/// <summary>
/// Base DTO for auditable entities.
/// </summary>
public abstract record AuditableDto
{
    public DateTimeOffset CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTimeOffset? ModifiedAt { get; init; }
    public string? ModifiedBy { get; init; }
}
