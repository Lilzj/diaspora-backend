using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.ValueObjects;

public sealed class AuditInfo : ValueObject
{
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    private AuditInfo() { }

    public static AuditInfo Create(DateTimeOffset createdAt, string? createdBy = null) =>
        new() { CreatedAt = createdAt, CreatedBy = createdBy };

    public AuditInfo MarkModified(DateTimeOffset modifiedAt, string? modifiedBy = null)
    {
        ModifiedAt = modifiedAt;
        ModifiedBy = modifiedBy;
        return this;
    }

    public AuditInfo MarkDeleted(DateTimeOffset deletedAt, string? deletedBy = null)
    {
        DeletedAt = deletedAt;
        DeletedBy = deletedBy;
        return this;
    }

    public bool IsDeleted => DeletedAt.HasValue;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CreatedAt;
        yield return CreatedBy;
        yield return ModifiedAt;
        yield return ModifiedBy;
        yield return DeletedAt;
        yield return DeletedBy;
    }
}
