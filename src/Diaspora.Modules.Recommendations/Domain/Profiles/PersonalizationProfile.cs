using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Events;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Profiles;

/// <summary>
/// What the platform believes about one member's tastes.
/// <para>
/// <b>Entirely derived, and deletable without loss.</b> Drop every row in this table and the
/// platform recovers completely from the signal store on the next build. That is not a nice
/// property to have — it is the guarantee that makes a personalization profile safe to hold at all,
/// because it means the profile is a cache of an opinion rather than a second copy of a member's
/// history. It is also what makes a GDPR erasure tractable: delete the signals and the profile has
/// nothing left to say.
/// </para>
/// <para>
/// <b>The member's stated preferences are deliberately not here.</b> They live in
/// <see cref="Preferences.PersonalizationPreference"/>, one aggregate over, because a rebuild
/// recomputes everything in this class from scratch — and a rebuild that discarded the categories a
/// member explicitly muted would be a privacy failure dressed as a refresh. The separation makes
/// that mistake unexpressible rather than merely avoided.
/// </para>
/// <para>
/// <b><see cref="IsColdStart"/> is a stored decision rather than a computed one.</b> It could be
/// derived — "no interests and fewer than five signals" — but deriving it means every strategy
/// re-derives it slightly differently, and the one thing every cold-start path must agree on is
/// whether it is running.
/// </para>
/// <para>
/// <b><see cref="Revision"/> is the concurrency token.</b> Two consumers folding signals into one
/// profile concurrently is the normal case, not the exception: a member who applies for a job while
/// the sweep is rebuilding them is a Tuesday afternoon.
/// </para>
/// </summary>
public sealed class PersonalizationProfile : AggregateRoot<PersonalizationProfileId>, IAuditableEntity
{
    /// <summary>Below this many signals the profile is treated as cold however complete it looks.</summary>
    public const int ColdStartSignalFloor = 5;

    /// <summary>How many interests one profile may hold. The tail is noise and it costs a join.</summary>
    public const int MaximumInterests = 200;

    /// <summary>How many location affinities one profile may hold.</summary>
    public const int MaximumLocationAffinities = 50;

    private readonly List<ProfileInterest> _interests = [];
    private readonly List<ProfileLocationAffinity> _locationAffinities = [];
    private readonly List<ProfileActivitySummary> _activitySummaries = [];

    private PersonalizationProfile(
        PersonalizationProfileId id, UserId userId, DateTimeOffset createdAt) : base(id)
    {
        UserId = userId;
        CreatedAt = createdAt;
        IsColdStart = true;
    }

    private PersonalizationProfile() { }

    public UserId UserId { get; private set; } = default!;

    /// <summary>BCP 47, lower case. From Identity, never guessed from what the member reads.</summary>
    public string? PreferredLanguage { get; private set; }

    /// <summary>ISO 3166-1 alpha-2, upper case. Where the member actually is.</summary>
    public string? PrimaryCountryCode { get; private set; }

    public string? PrimaryCityName { get; private set; }

    public string? PrimaryNeighborhoodName { get; private set; }

    /// <summary>As Trust reported it. Never inferred here, and never adjusted by this module.</summary>
    public int TrustLevel { get; private set; }

    public bool IsVerified { get; private set; }

    /// <summary>False keeps the member on the onboarding-aware feed. From Identity.</summary>
    public bool HasCompletedOnboarding { get; private set; }

    /// <summary>See the class summary — stored rather than derived, on purpose.</summary>
    public bool IsColdStart { get; private set; }

    /// <summary>How many signals have ever been folded in. The denominator of confidence.</summary>
    public int SignalCount { get; private set; }

    /// <summary>0–100. What fraction of the things a good profile has, this one has.</summary>
    public int Completeness { get; private set; }

    public DateTimeOffset? LastSignalAt { get; private set; }

    /// <summary>When any build last touched this. Drives the staleness sweep.</summary>
    public DateTimeOffset? LastBuiltAt { get; private set; }

    /// <summary>When a full recompute last ran. Drives the rebuild cooldown.</summary>
    public DateTimeOffset? LastRebuiltAt { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<ProfileInterest> Interests => _interests.AsReadOnly();

    public IReadOnlyList<ProfileLocationAffinity> LocationAffinities => _locationAffinities.AsReadOnly();

    public IReadOnlyList<ProfileActivitySummary> ActivitySummaries => _activitySummaries.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static PersonalizationProfile Open(
        PersonalizationProfileId id, UserId userId, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);

        return new PersonalizationProfile(id, userId, createdAt);
    }

    // ── Identity, Trust and Location facts ──────────────────────────────────────────────────

    /// <summary>
    /// Records where the member is, as their own modules reported it.
    /// <para>
    /// Sets the primary affinity at each grain it is given, and clears the previous primary at that
    /// grain — a member who moves from Peckham to Grand Baie must stop being primarily in Peckham,
    /// and leaving two primaries would make the location strategy score both at its ceiling.
    /// </para>
    /// </summary>
    public void SetLocation(
        string? countryCode,
        string? cityName,
        string? neighborhoodName,
        DateTimeOffset observedAt,
        Func<AffinityScope, string, Guid> affinityIdFactory)
    {
        ArgumentNullException.ThrowIfNull(affinityIdFactory);

        PrimaryCountryCode = string.IsNullOrWhiteSpace(countryCode)
            ? PrimaryCountryCode
            : countryCode.Trim().ToUpperInvariant();

        PrimaryCityName = string.IsNullOrWhiteSpace(cityName) ? PrimaryCityName : cityName.Trim();

        PrimaryNeighborhoodName = string.IsNullOrWhiteSpace(neighborhoodName)
            ? PrimaryNeighborhoodName
            : neighborhoodName.Trim();

        PromotePrimary(AffinityScope.Country, PrimaryCountryCode, PrimaryCountryCode, observedAt, affinityIdFactory);
        PromotePrimary(AffinityScope.City, PrimaryCityName, PrimaryCityName, observedAt, affinityIdFactory);
        PromotePrimary(
            AffinityScope.Neighborhood,
            PrimaryNeighborhoodName,
            PrimaryNeighborhoodName,
            observedAt,
            affinityIdFactory);

        Touch();
    }

    public void SetLanguage(string? languageCode)
    {
        PreferredLanguage = string.IsNullOrWhiteSpace(languageCode)
            ? PreferredLanguage
            : languageCode.Trim().ToLowerInvariant();

        Touch();
    }

    public void SetTrust(int trustLevel, bool isVerified)
    {
        TrustLevel = Math.Max(0, trustLevel);
        IsVerified = isVerified;
        Touch();
    }

    public void SetOnboardingComplete(bool hasCompleted)
    {
        HasCompletedOnboarding = hasCompleted;
        Touch();
    }

    // ── Folding signals in ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Folds one interest observation in, creating the interest on first sight.
    /// <para>
    /// Returns silently when the profile is already at its interest ceiling and the key is new. The
    /// alternative — evicting the weakest to make room — would let a burst of noise displace a
    /// year's worth of established affinity, which is the failure mode a ceiling exists to prevent.
    /// </para>
    /// </summary>
    public void ObserveInterest(
        InterestKind kind, string key, decimal strength, DateTimeOffset observedAt, Guid interestId)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var normalized = key.Trim().ToLowerInvariant();
        var existing = FindInterest(kind, normalized);

        if (existing is null)
        {
            if (_interests.Count >= MaximumInterests)
            {
                return;
            }

            existing = ProfileInterest.Create(interestId, kind, normalized);
            _interests.Add(existing);
        }

        existing.Observe(strength, observedAt);
        Touch();
    }

    /// <summary>Records an interest the member stated. Never decayed, never pruned.</summary>
    public void DeclareInterest(InterestKind kind, string key, DateTimeOffset observedAt, Guid interestId)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var normalized = key.Trim().ToLowerInvariant();
        var existing = FindInterest(kind, normalized);

        if (existing is null)
        {
            existing = ProfileInterest.Create(interestId, kind, normalized, isExplicit: true);
            _interests.Add(existing);
        }
        else
        {
            existing.MarkExplicit();
        }

        existing.Observe(1m, observedAt);
        Touch();
    }

    public void ObserveLocation(
        AffinityScope scope,
        string key,
        string? displayName,
        decimal strength,
        DateTimeOffset observedAt,
        Guid affinityId)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var existing = EnsureAffinity(scope, key, displayName, affinityId);

        if (existing is null)
        {
            return;
        }

        existing.Observe(strength, observedAt);
        Touch();
    }

    public void ObserveActivity(
        RecommendationTargetType targetType, SignalKind kind, DateTimeOffset occurredAt, Guid summaryId)
    {
        var summary = _activitySummaries.Find(entry => entry.TargetType == targetType);

        if (summary is null)
        {
            summary = ProfileActivitySummary.Create(summaryId, targetType);
            _activitySummaries.Add(summary);
        }

        summary.Record(kind, occurredAt);
        Touch();
    }

    /// <summary>Records that a batch of signals was folded in, and re-derives the cheap fields.</summary>
    public void RecordSignalsFolded(int count, DateTimeOffset lastSignalAt, DateTimeOffset builtAt)
    {
        SignalCount += Math.Max(0, count);

        if (LastSignalAt is null || lastSignalAt > LastSignalAt)
        {
            LastSignalAt = lastSignalAt;
        }

        LastBuiltAt = builtAt;

        Recompute();
        Touch();
    }

    /// <summary>
    /// Ages every derived affinity towards zero.
    /// <para>
    /// Called by the builder before folding a new window in, so a profile that is rebuilt often
    /// decays often and one that is not is stale rather than wrong. Explicit interests and the
    /// member's actual location are exempt, because neither is an inference.
    /// </para>
    /// </summary>
    public void Decay(decimal factor)
    {
        foreach (var interest in _interests)
        {
            interest.Decay(factor);
        }

        foreach (var affinity in _locationAffinities)
        {
            affinity.Decay(factor);
        }

        Touch();
    }

    /// <summary>
    /// Empties every derived collection, ahead of a full recompute.
    /// <para>
    /// <b>Explicit interests survive.</b> A rebuild recomputes what the platform inferred; it must
    /// not discard what the member said, and the two are distinguishable only because
    /// <see cref="ProfileInterest.IsExplicit"/> exists.
    /// </para>
    /// </summary>
    public void BeginRebuild()
    {
        _interests.RemoveAll(interest => !interest.IsExplicit);
        _locationAffinities.RemoveAll(affinity => !affinity.IsPrimary);

        foreach (var summary in _activitySummaries)
        {
            summary.Reset();
        }

        SignalCount = 0;
        LastSignalAt = null;

        Touch();
    }

    /// <summary>Closes a full recompute and raises the event a rebuild is expected to raise.</summary>
    public void CompleteRebuild(DateTimeOffset rebuiltAt)
    {
        LastRebuiltAt = rebuiltAt;
        LastBuiltAt = rebuiltAt;

        Recompute();
        Touch();

        RaiseDomainEvent(new PersonalizationProfileUpdatedDomainEvent
        {
            ProfileId = Id,
            UserId = UserId,
            Completeness = Completeness,
            IsColdStart = IsColdStart,
            SignalCount = SignalCount,
            WasRebuilt = true,
            UpdatedAt = rebuiltAt
        });
    }

    /// <summary>
    /// Announces an incremental update.
    /// <para>
    /// Separate from <see cref="RecordSignalsFolded"/> so a consumer folding one signal at a time
    /// does not raise an event per signal. The caller decides when a run of folds is worth telling
    /// anybody about, which is a decision the aggregate has no information to make.
    /// </para>
    /// </summary>
    public void AnnounceUpdate(DateTimeOffset updatedAt)
    {
        RaiseDomainEvent(new PersonalizationProfileUpdatedDomainEvent
        {
            ProfileId = Id,
            UserId = UserId,
            Completeness = Completeness,
            IsColdStart = IsColdStart,
            SignalCount = SignalCount,
            WasRebuilt = false,
            UpdatedAt = updatedAt
        });
    }

    // ── Reading ─────────────────────────────────────────────────────────────────────────────

    public decimal AffinityFor(InterestKind kind, string key) =>
        FindInterest(kind, key.Trim().ToLowerInvariant())?.Affinity ?? 0m;

    public decimal LocationAffinityFor(AffinityScope scope, string key)
    {
        var normalized = scope == AffinityScope.Country
            ? key.Trim().ToUpperInvariant()
            : key.Trim().ToLowerInvariant();

        return _locationAffinities
            .Find(affinity => affinity.Scope == scope &&
                              string.Equals(affinity.Key, normalized, StringComparison.Ordinal))
            ?.Affinity ?? 0m;
    }

    public ProfileActivitySummary? ActivityFor(RecommendationTargetType targetType) =>
        _activitySummaries.Find(summary => summary.TargetType == targetType);

    /// <summary>The strongest interests, most affine first. What the reason builder explains from.</summary>
    public IReadOnlyList<ProfileInterest> TopInterests(int count) =>
        _interests
            .Where(interest => interest.Affinity > 0m)
            .OrderByDescending(interest => interest.Affinity)
            .ThenByDescending(interest => interest.ObservationCount)
            .ThenBy(interest => interest.Key, StringComparer.Ordinal)
            .Take(Math.Max(0, count))
            .ToList();

    /// <summary>The distinct kinds of thing this profile knows about. Feeds the confidence rating.</summary>
    public int DistinctInterestKinds =>
        _interests.Where(interest => interest.Affinity > 0m).Select(interest => interest.Kind).Distinct().Count();

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    // ── Internals ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Re-derives completeness and the cold-start decision.
    /// <para>
    /// <b>Completeness is six things the platform would like to know, weighted by how much each one
    /// improves a recommendation.</b> It is not a percentage of anything real, and it is not
    /// presented to members as one — it drives the sweep's priority ordering and the confidence
    /// rating, both of which need a single comparable number.
    /// </para>
    /// </summary>
    private void Recompute()
    {
        var score = 0;

        if (!string.IsNullOrWhiteSpace(PrimaryCountryCode)) score += 25;
        if (!string.IsNullOrWhiteSpace(PrimaryCityName)) score += 10;
        if (!string.IsNullOrWhiteSpace(PrimaryNeighborhoodName)) score += 5;
        if (!string.IsNullOrWhiteSpace(PreferredLanguage)) score += 10;
        if (_interests.Exists(interest => interest.Affinity > 0m)) score += 25;
        if (_activitySummaries.Exists(summary => summary.TotalCount > 0)) score += 15;
        if (SignalCount >= ColdStartSignalFloor * 4) score += 10;

        Completeness = Math.Clamp(score, 0, 100);

        // Cold start is about evidence, not about fields. A member whose country arrived from
        // Identity has 25% completeness and still nothing the platform has seen them do.
        IsColdStart = SignalCount < ColdStartSignalFloor
                      || !_interests.Exists(interest => interest.Affinity > 0m);
    }

    private ProfileInterest? FindInterest(InterestKind kind, string normalizedKey) =>
        _interests.Find(interest =>
            interest.Kind == kind && string.Equals(interest.Key, normalizedKey, StringComparison.Ordinal));

    private ProfileLocationAffinity? EnsureAffinity(
        AffinityScope scope, string key, string? displayName, Guid affinityId)
    {
        var normalized = scope == AffinityScope.Country
            ? key.Trim().ToUpperInvariant()
            : key.Trim().ToLowerInvariant();

        var existing = _locationAffinities.Find(affinity =>
            affinity.Scope == scope && string.Equals(affinity.Key, normalized, StringComparison.Ordinal));

        if (existing is not null)
        {
            return existing;
        }

        if (_locationAffinities.Count >= MaximumLocationAffinities)
        {
            return null;
        }

        var created = ProfileLocationAffinity.Create(affinityId, scope, normalized, displayName);
        _locationAffinities.Add(created);

        return created;
    }

    private void PromotePrimary(
        AffinityScope scope,
        string? key,
        string? displayName,
        DateTimeOffset observedAt,
        Func<AffinityScope, string, Guid> affinityIdFactory)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        foreach (var affinity in _locationAffinities.Where(entry => entry.Scope == scope))
        {
            affinity.ClearPrimary();
        }

        var target = EnsureAffinity(scope, key, displayName, affinityIdFactory(scope, key));

        target?.MarkPrimary(displayName, observedAt);
    }

    private void Touch() => Revision++;
}
