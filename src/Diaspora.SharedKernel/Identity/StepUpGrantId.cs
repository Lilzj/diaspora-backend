using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record StepUpGrantId(Guid Value) : StronglyTypedId(Value)
{
    public static StepUpGrantId New() => new(Guid.NewGuid());
    public static StepUpGrantId From(Guid value) => new(value);
}
