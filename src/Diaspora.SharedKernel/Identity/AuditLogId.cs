using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record AuditLogId(Guid Value) : StronglyTypedId(Value)
{
    public static AuditLogId New() => new(Guid.NewGuid());
    public static AuditLogId From(Guid value) => new(value);
}
