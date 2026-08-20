using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Identifiers;

/// <summary>A saved-listing row's identity.</summary>
public sealed record SavedListingId(Guid Value) : StronglyTypedId(Value)
{
    public static SavedListingId New() => new(Guid.NewGuid());

    public static SavedListingId From(Guid value) => new(value);
}
