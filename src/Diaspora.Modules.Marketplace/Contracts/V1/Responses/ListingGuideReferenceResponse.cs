namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// A guide shown beside the listing — how to buy a used car safely, what an import duty costs.
/// <para>
/// The title comes from B8 at read time, so a listing never renders a link to a guide that has
/// since been unpublished. A copied title would do exactly that.
/// </para>
/// </summary>
public sealed record ListingGuideReferenceResponse
{
    public required Guid GuideId { get; init; }

    /// <summary>The guide's current title, from B8. Falls back to the stored label.</summary>
    public string? Title { get; init; }

    /// <summary>The deep-link handle, from B8.</summary>
    public string? Slug { get; init; }

    /// <summary>Whether the platform suggested it rather than the seller choosing it.</summary>
    public required bool IsAutomatic { get; init; }
}
