using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One donation, monetary or otherwise, from one giver to one cause.</summary>
public sealed record DonationId(Guid Value) : StronglyTypedId(Value)
{
    public static DonationId New() => new(Guid.NewGuid());

    public static DonationId From(Guid value) => new(value);
}
