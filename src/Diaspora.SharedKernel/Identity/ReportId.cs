using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record ReportId(Guid Value) : StronglyTypedId(Value)
{
    public static ReportId New() => new(Guid.NewGuid());
    public static ReportId From(Guid value) => new(value);
}
