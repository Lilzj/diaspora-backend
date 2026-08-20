using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Profiles;

/// <summary>
/// A place the member recently switched to, kept so the selector can offer it again.
/// <para>
/// Ordered by <see cref="VisitedAt"/> and capped by the owning profile. Distinct from a
/// <see cref="SavedLocation"/> in intent: a recent is something the member did, a saved is
/// something they decided.
/// </para>
/// </summary>
public sealed class RecentLocation : Entity<RecentLocationId>
{
    public LocationProfileId ProfileId { get; private set; } = default!;

    public LocationSnapshot Location { get; private set; } = default!;

    public DateTimeOffset VisitedAt { get; private set; }

    private RecentLocation() { }

    private RecentLocation(
        RecentLocationId id,
        LocationProfileId profileId,
        LocationSnapshot location,
        DateTimeOffset visitedAt) : base(id)
    {
        ProfileId = profileId;
        Location = location;
        VisitedAt = visitedAt;
    }

    internal static RecentLocation Create(
        LocationProfileId profileId, LocationSnapshot location, DateTimeOffset visitedAt)
    {
        ArgumentNullException.ThrowIfNull(profileId);
        ArgumentNullException.ThrowIfNull(location);

        return new RecentLocation(RecentLocationId.New(), profileId, location, visitedAt);
    }

    internal void Touch(DateTimeOffset visitedAt) => VisitedAt = visitedAt;
}
