namespace Diaspora.Domain.Abstractions;

public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; }
    string? CreatedBy { get; }
    DateTimeOffset? ModifiedAt { get; }
    string? ModifiedBy { get; }

    void SetCreated(DateTimeOffset timestamp, string? userId);
    void SetModified(DateTimeOffset timestamp, string? userId);
}
