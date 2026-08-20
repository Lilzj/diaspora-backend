namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Puts a property on the market.
/// <para>
/// The body is optional. A publish with nothing in it is the common case, and the resulting state
/// depends on the property rather than on the request: available when the move-in date has arrived,
/// published when it has not.
/// </para>
/// </summary>
public sealed record PublishHousingListingRequest
{
    /// <summary>
    /// Restates the move-in date as part of publishing, which is exactly when a landlord fixes it.
    /// Forcing a separate call would leave the common case as two round trips.
    /// </summary>
    public DateTimeOffset? AvailableFrom { get; init; }

    /// <summary>Public or Unlisted. Unlisted is reachable by link and never appears in search.</summary>
    public string? Visibility { get; init; }
}
