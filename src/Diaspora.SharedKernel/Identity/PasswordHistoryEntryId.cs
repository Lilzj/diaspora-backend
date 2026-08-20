using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.Identity;

public sealed record PasswordHistoryEntryId(Guid Value) : StronglyTypedId(Value)
{
    public static PasswordHistoryEntryId New() => new(Guid.NewGuid());
    public static PasswordHistoryEntryId From(Guid value) => new(value);
    public static readonly PasswordHistoryEntryId Empty = new(Guid.Empty);
}
