using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Identifiers;

/// <summary>
/// A negotiation's identity.
/// <para>
/// <c>ListingOfferId</c> rather than <c>OfferId</c>, for the reason B9 named its own
/// <c>JobOfferId</c>: "offer" is a word several verticals want, and a module-prefixed identifier
/// is what keeps two of them from meaning different things by the same name.
/// </para>
/// </summary>
public sealed record ListingOfferId(Guid Value) : StronglyTypedId(Value)
{
    public static ListingOfferId New() => new(Guid.NewGuid());

    public static ListingOfferId From(Guid value) => new(value);
}
