using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>Something that can be given to. Reference data owned here, not by any vertical.</summary>
public sealed record DonationCauseId(Guid Value) : StronglyTypedId(Value)
{
    public static DonationCauseId New() => new(Guid.NewGuid());

    public static DonationCauseId From(Guid value) => new(value);
}
