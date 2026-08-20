using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Identifiers;

/// <summary>A résumé version's identity.</summary>
public sealed record ResumeId(Guid Value) : StronglyTypedId(Value)
{
    public static ResumeId New() => new(Guid.NewGuid());

    public static ResumeId From(Guid value) => new(value);
}
