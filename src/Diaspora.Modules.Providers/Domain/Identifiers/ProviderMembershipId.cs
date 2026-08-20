using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Identifiers;

/// <summary>Identifies one person's role on a provider: owner, manager, moderator, editor or contributor.</summary>
public sealed record ProviderMembershipId(Guid Value) : StronglyTypedId(Value)
{
    public static ProviderMembershipId New() => new(Guid.NewGuid());

    public static ProviderMembershipId From(Guid value) => new(value);
}
