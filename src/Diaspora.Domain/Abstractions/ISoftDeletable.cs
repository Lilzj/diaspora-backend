namespace Diaspora.Domain.Abstractions;

public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; }
    string? DeletedBy { get; }
    bool IsDeleted { get; }

    void MarkDeleted(DateTimeOffset timestamp, string? userId);
    void Restore();
}
