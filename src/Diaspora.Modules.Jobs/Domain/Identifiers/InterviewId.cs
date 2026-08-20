using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Identifiers;

/// <summary>An interview's identity. One round, against one application.</summary>
public sealed record InterviewId(Guid Value) : StronglyTypedId(Value)
{
    public static InterviewId New() => new(Guid.NewGuid());

    public static InterviewId From(Guid value) => new(value);
}
