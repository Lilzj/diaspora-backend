using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>Where a member's money goes when it leaves. Never a credential.</summary>
public sealed record PayoutDestinationId(Guid Value) : StronglyTypedId(Value)
{
    public static PayoutDestinationId New() => new(Guid.NewGuid());

    public static PayoutDestinationId From(Guid value) => new(value);
}
