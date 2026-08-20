using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Identifiers;

/// <summary>
/// A marketplace category's identity.
/// <para>
/// Named <c>ListingCategoryId</c> rather than <c>CategoryId</c> because the kernel's identifier
/// bag already declares the latter, and B9 records what happens when a module ignores that: a
/// collision resolved by a <c>using</c> alias at the top of forty files. Prefixing avoids the
/// collision rather than managing it.
/// </para>
/// </summary>
public sealed record ListingCategoryId(Guid Value) : StronglyTypedId(Value)
{
    public static ListingCategoryId New() => new(Guid.NewGuid());

    public static ListingCategoryId From(Guid value) => new(value);
}
