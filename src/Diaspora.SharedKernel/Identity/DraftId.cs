using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record DraftId(Guid Value) : StronglyTypedId(Value)
{
    public static DraftId New() => new(Guid.NewGuid());
    public static DraftId From(Guid value) => new(value);
}
