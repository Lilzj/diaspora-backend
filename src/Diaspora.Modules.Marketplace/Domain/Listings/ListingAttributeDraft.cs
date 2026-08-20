namespace Diaspora.Modules.Marketplace.Domain.Listings;

/// <summary>
/// One attribute as a caller supplies it, before the aggregate has validated the set.
/// <para>
/// A flat record so a handler never constructs a domain object and never has to know how one is
/// validated. The aggregate turns a list of these into <see cref="ListingAttribute"/> rows,
/// enforces the category's requirements across the whole set, and reports one error.
/// </para>
/// </summary>
public sealed record ListingAttributeDraft
{
    public required string Key { get; init; }

    public required string Value { get; init; }

    /// <summary>The label to render, when the caller has a better one than the key.</summary>
    public string? DisplayName { get; init; }
}
