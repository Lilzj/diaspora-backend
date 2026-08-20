using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Providers.V1.Events;

/// <summary>
/// A provider became visible to somebody other than its own staff.
/// <para>
/// <b>The event most consumers actually want.</b> This is when a provider enters search (B15),
/// becomes recommendable (B17), and is worth telling a neighbourhood about (B16). It fires once:
/// a provider published, paused and published again raises
/// <see cref="ProviderStatusChangedIntegrationEvent"/> the second time, because the second time
/// is not news.
/// </para>
/// </summary>
public sealed record ProviderPublishedIntegrationEvent : IntegrationEvent
{
    public required Guid ProviderId { get; init; }

    public required Guid OwnerId { get; init; }

    public required string ProviderType { get; init; }

    public required Guid CategoryId { get; init; }

    public required string Name { get; init; }

    /// <summary>The <c>ProviderVisibilityLevel</c> name.</summary>
    public required string Visibility { get; init; }

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>
    /// The category's stable slug — <c>plumbing</c>, <c>legal-services</c> — beside the identifier
    /// above. Added in PSR-1.
    /// <para>
    /// <b>The field that makes a provider's category mean something to a consumer.</b> Every other
    /// vertical publishes a human-readable category key: Jobs sends <c>CategoryKey</c>, Marketplace
    /// sends it on both publication and sale. This contract sent only a <see cref="CategoryId"/>,
    /// so a consumer grouping by category grouped by GUID — and B14's recommendation engine, which
    /// matches a member's stated interest in "plumbing" against a candidate's category, could never
    /// match one. It stored the GUID in a field named <c>CategoryKey</c> and recorded the mismatch
    /// in a comment.
    /// </para>
    /// <para>
    /// <b>The identifier is unchanged and still required.</b> This is additive: a consumer that
    /// resolves the identifier keeps working, and a consumer that wants to match on meaning now
    /// can.
    /// </para>
    /// <para>
    /// Null on events published before PSR-1, and null when the category could not be read at
    /// publication. A consumer that finds it null should fall back to the identifier rather than
    /// treat the provider as uncategorised.
    /// </para>
    /// </summary>
    public string? CategoryKey { get; init; }
}
