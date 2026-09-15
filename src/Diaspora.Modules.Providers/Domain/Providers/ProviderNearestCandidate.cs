using Diaspora.Modules.Providers.Domain.Identifiers;

namespace Diaspora.Modules.Providers.Domain.Providers;

/// <summary>
/// Just enough of a provider to place it in a "nearest first" search: its identifier, its slug for
/// the tie-break, and its position if it has one. A provider with no position can still match a
/// radius through its service areas, and sorts after every one that can be measured.
/// </summary>
public sealed record ProviderNearestCandidate(
    ProviderId Id,
    string Slug,
    double? Latitude,
    double? Longitude);
