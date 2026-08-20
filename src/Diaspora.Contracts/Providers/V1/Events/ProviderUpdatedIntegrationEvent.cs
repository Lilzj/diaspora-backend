using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Providers.V1.Events;

/// <summary>
/// A provider's descriptive or operational content changed.
/// <para>
/// <see cref="ChangedFields"/> is what makes this event usable rather than merely present. A
/// consumer that reindexes on every provider edit reindexes on a gallery reorder; one that reads
/// the field list reindexes only when something searchable moved. The names are the response
/// DTO's field names, so a consumer never has to know B7's internal property names.
/// </para>
/// </summary>
public sealed record ProviderUpdatedIntegrationEvent : IntegrationEvent
{
    public required Guid ProviderId { get; init; }

    /// <summary>Null when a projection updated it from another module's event.</summary>
    public Guid? ChangedBy { get; init; }

    /// <summary>Wire field names that moved: <c>name</c>, <c>description</c>, <c>category</c>.</summary>
    public required IReadOnlyList<string> ChangedFields { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>
    /// The provider's notifiable member — B13.1. Its current primary owner.
    /// <para>
    /// Distinct from <see cref="ChangedBy"/>, which is whoever made the edit and may be any
    /// member of the provider's staff or none of them. A consumer that conflated the two would
    /// tell a receptionist that their own edit had been saved and tell the owner nothing.
    /// </para>
    /// <para>Null on events published before B13.1, and null when the provider could not be read.</para>
    /// </summary>
    public Guid? OwnerUserId { get; init; }
}
