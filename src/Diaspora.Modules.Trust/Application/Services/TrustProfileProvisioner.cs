using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Trust.Application.Services;

/// <summary>
/// Gets a member's trust and reputation profiles, creating them on first contact.
/// <para>
/// Trust deliberately does not subscribe to <c>UserRegistered</c> to pre-create profiles.
/// Doing so would mean every account that never touches a trust feature still carries two
/// rows, and — more importantly — it would make Trust's data depend on having observed an
/// Identity event, so a replayed or missed event leaves a member permanently unable to
/// verify. Creating on demand is idempotent and needs no delivery guarantee.
/// </para>
/// </summary>
public sealed class TrustProfileProvisioner
{
    private readonly ITrustProfileRepository _profiles;
    private readonly IReputationProfileRepository _reputations;
    private readonly ITrustEventRepository _timeline;
    private readonly IClock _clock;

    public TrustProfileProvisioner(
        ITrustProfileRepository profiles,
        IReputationProfileRepository reputations,
        ITrustEventRepository timeline,
        IClock clock)
    {
        _profiles = profiles;
        _reputations = reputations;
        _timeline = timeline;
        _clock = clock;
    }

    /// <summary>The member's trust profile, created if this is their first trust action.</summary>
    public async Task<TrustProfile> GetOrCreateAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var existing = await _profiles.GetByUserAsync(userId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var now = _clock.UtcNow;
        var profile = TrustProfile.Open(userId, now);
        await _profiles.AddAsync(profile, cancellationToken);

        // The first entry on the member's trust journey. The client's seeded store opens
        // with exactly this event, so a real account's timeline starts the same way a
        // mocked one did.
        await _timeline.AddAsync(
            TrustEvent.Record(
                userId,
                TrustEventTypes.Joined,
                "Joined Diaspora",
                now,
                "Your trust profile was created.",
                TrustEventTypes.IconFor(TrustEventTypes.Joined)),
            cancellationToken);

        return profile;
    }

    /// <summary>The member's reputation profile, created if absent.</summary>
    public async Task<ReputationProfile> GetOrCreateReputationAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var existing = await _reputations.GetByUserAsync(userId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var reputation = ReputationProfile.Open(userId, _clock.UtcNow);
        await _reputations.AddAsync(reputation, cancellationToken);

        return reputation;
    }

    /// <summary>Both profiles in one call — the common case for a command that mutates trust.</summary>
    public async Task<(TrustProfile Trust, ReputationProfile Reputation)> GetOrCreateBothAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        var trust = await GetOrCreateAsync(userId, cancellationToken);
        var reputation = await GetOrCreateReputationAsync(userId, cancellationToken);

        return (trust, reputation);
    }
}
