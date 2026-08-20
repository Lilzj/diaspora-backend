using System.Diagnostics;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Preferences;
using Diaspora.Modules.Recommendations.Domain.Profiles;
using Diaspora.Modules.Recommendations.Domain.Signals;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.Modules.Recommendations.Observability;
using Diaspora.Modules.Recommendations.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Recommendations.Infrastructure.Personalization;

/// <summary>
/// Turns signals into a profile, deterministically.
/// <para>
/// <b>The same signals in the same order always produce the same profile, and that is a property
/// worth paying for.</b> It means a member's profile can be reproduced from the signal table for an
/// audit, that a defect in ranking can be reproduced from the data that caused it, and that a
/// rebuild is a repair rather than a roll of the dice.
/// </para>
/// <para>
/// <b>Age is applied at fold time rather than by a timer.</b> A signal's contribution is its weight
/// decayed by its own kind's half-life, computed against the moment of the build — so a profile that
/// has not been rebuilt for a month is stale rather than silently wrong, which is the honest failure
/// of the two. See <c>UserSignal.EffectiveWeight</c>.
/// </para>
/// <para>
/// <b>A dismissal subtracts.</b> Every other kind folds in as evidence of interest; a dismissal is
/// evidence against, and treating it as just another observation would make a member who refuses
/// something ten times look like a member who is deeply engaged with it. It is the one kind that
/// moves an affinity downwards.
/// </para>
/// <para>
/// <b>The rebuild keeps what the member stated and discards what the platform inferred.</b> The
/// aggregate enforces the distinction — see <c>PersonalizationProfile.BeginRebuild</c> — so a
/// rebuild written by somebody who has not read this paragraph still cannot discard a stated
/// preference.
/// </para>
/// </summary>
public sealed class DefaultPersonalizationProfileBuilder : IPersonalizationProfileBuilder
{
    /// <summary>How much a full window's age costs an existing affinity on an incremental build.</summary>
    private const decimal IncrementalDecayFactor = 0.98m;

    private readonly IPersonalizationProfileRepository _profiles;
    private readonly IPersonalizationPreferenceRepository _preferences;
    private readonly ISignalRepository _signals;
    private readonly ISignalWeightPolicy _weights;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IOptions<RecommendationOptions> _options;

    public DefaultPersonalizationProfileBuilder(
        IPersonalizationProfileRepository profiles,
        IPersonalizationPreferenceRepository preferences,
        ISignalRepository signals,
        ISignalWeightPolicy weights,
        IGuidGenerator guids,
        IClock clock,
        IOptions<RecommendationOptions> options)
    {
        _profiles = profiles;
        _preferences = preferences;
        _signals = signals;
        _weights = weights;
        _guids = guids;
        _clock = clock;
        _options = options;
    }

    public async Task<PersonalizationProfile> BuildIncrementalAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var stopwatch = Stopwatch.StartNew();
        var now = _clock.UtcNow;

        var profile = await _profiles.GetOrCreateAsync(userId, cancellationToken);
        var pending = await _signals.ListUnprocessedAsync(
            userId, _options.Value.ProfileBuildSignalWindow, cancellationToken);

        if (pending.Count == 0)
        {
            return profile;
        }

        // Age what is already there before adding to it, so an old affinity and a new one are
        // compared on the same footing.
        profile.Decay(IncrementalDecayFactor);

        Fold(profile, pending, now);

        var preference = await _preferences.FindAsync(userId, cancellationToken);

        ApplyStatedPreferences(profile, preference, now);

        profile.RecordSignalsFolded(pending.Count, pending.Max(signal => signal.OccurredAt), now);
        profile.AnnounceUpdate(now);

        foreach (var signal in pending)
        {
            signal.MarkProcessed(now);
        }

        _profiles.Update(profile);

        stopwatch.Stop();

        RecommendationTelemetry.ProfilesBuilt.Add(1);
        RecommendationTelemetry.ProfileBuildDuration.Record(stopwatch.Elapsed.TotalMilliseconds);

        return profile;
    }

    public async Task<PersonalizationProfile> RebuildAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var stopwatch = Stopwatch.StartNew();
        var now = _clock.UtcNow;

        var profile = await _profiles.GetOrCreateAsync(userId, cancellationToken);
        var history = await _signals.ListForUserAsync(
            userId, _options.Value.ProfileBuildSignalWindow, cancellationToken);

        // Discards inferences, keeps what the member stated. Enforced by the aggregate.
        profile.BeginRebuild();

        if (history.Count > 0)
        {
            // Oldest first, so the exponential fold reaches the same answer it would have reached
            // incrementally. Folding newest-first would give the oldest signals the most influence,
            // which is the opposite of what the decay is for.
            Fold(profile, history.OrderBy(signal => signal.OccurredAt).ToList(), now);

            profile.RecordSignalsFolded(history.Count, history.Max(signal => signal.OccurredAt), now);
        }

        var preference = await _preferences.FindAsync(userId, cancellationToken);

        ApplyStatedPreferences(profile, preference, now);

        profile.CompleteRebuild(now);

        foreach (var signal in history)
        {
            signal.MarkProcessed(now);
        }

        _profiles.Update(profile);

        stopwatch.Stop();

        RecommendationTelemetry.ProfilesRebuilt.Add(1);
        RecommendationTelemetry.ProfileBuildDuration.Record(stopwatch.Elapsed.TotalMilliseconds);

        return profile;
    }

    /// <summary>
    /// Folds a window of signals into a profile.
    /// <para>
    /// One pass, one switch. Every kind ends up in at most three places: the profile's scalar facts,
    /// an interest or location affinity, and the activity summary for its target type.
    /// </para>
    /// </summary>
    private void Fold(
        PersonalizationProfile profile, IReadOnlyList<UserSignal> signals, DateTimeOffset now)
    {
        foreach (var signal in signals)
        {
            var strength = signal.EffectiveWeight(now, _weights.HalfLifeFor(signal.Kind));

            switch (signal.Kind)
            {
                case SignalKind.Location:
                    profile.SetLocation(
                        signal.Context.CountryCode,
                        signal.Context.CityName,
                        signal.Context.NeighborhoodName,
                        signal.OccurredAt,
                        (scope, key) => AffinityId(scope, key));
                    break;

                case SignalKind.Language:
                    profile.SetLanguage(signal.Key ?? signal.Context.LanguageCode);
                    break;

                case SignalKind.Trust:
                    profile.SetTrust(ParseLevel(signal.Key), profile.IsVerified);
                    break;

                case SignalKind.Verification:
                    profile.SetTrust(
                        profile.TrustLevel,
                        string.Equals(signal.Key, "verified", StringComparison.OrdinalIgnoreCase));
                    break;

                case SignalKind.Onboarding:
                    profile.SetOnboardingComplete(
                        string.Equals(signal.Key, "complete", StringComparison.OrdinalIgnoreCase));
                    break;

                case SignalKind.Interest:
                    // The member said so. Never decayed, never pruned.
                    if (signal.Key is not null)
                    {
                        profile.DeclareInterest(
                            signal.KeyKind ?? InterestKind.Category,
                            signal.Key,
                            signal.OccurredAt,
                            _guids.NewSequentialGuid());
                    }

                    break;

                case SignalKind.RecommendationDismissed:
                    // The one kind that moves an affinity down. See the class summary.
                    FoldDismissal(profile, signal, strength);
                    break;

                default:
                    FoldObservation(profile, signal, strength);
                    break;
            }

            // The location a behavioural signal happened in is an observation about where the member
            // keeps looking, distinct from where they are. See ProfileLocationAffinity.
            FoldContextLocation(profile, signal, strength);
        }
    }

    private void FoldObservation(
        PersonalizationProfile profile, UserSignal signal, decimal strength)
    {
        if (signal.Key is not null)
        {
            profile.ObserveInterest(
                signal.KeyKind ?? InterestKind.Category,
                signal.Key,
                strength,
                signal.OccurredAt,
                _guids.NewSequentialGuid());
        }

        if (signal.TargetType is { } targetType)
        {
            profile.ObserveActivity(
                targetType, signal.Kind, signal.OccurredAt, _guids.NewSequentialGuid());

            profile.ObserveInterest(
                InterestKind.TargetType,
                targetType.ToString(),
                strength,
                signal.OccurredAt,
                _guids.NewSequentialGuid());
        }
    }

    /// <summary>
    /// Records a refusal.
    /// <para>
    /// Observed at zero rather than skipped: the affinity's exponential fold moves towards whatever
    /// it is given, so an observation of zero pulls it down by exactly the learning rate. That is a
    /// gentler correction than removing the interest outright, which would let one accidental swipe
    /// erase a year of evidence.
    /// </para>
    /// </summary>
    private void FoldDismissal(
        PersonalizationProfile profile, UserSignal signal, decimal strength)
    {
        _ = strength;

        if (signal.Key is not null)
        {
            profile.ObserveInterest(
                signal.KeyKind ?? InterestKind.Category,
                signal.Key,
                0m,
                signal.OccurredAt,
                _guids.NewSequentialGuid());
        }

        if (signal.TargetType is { } targetType)
        {
            profile.ObserveActivity(
                targetType, signal.Kind, signal.OccurredAt, _guids.NewSequentialGuid());
        }
    }

    private void FoldContextLocation(
        PersonalizationProfile profile, UserSignal signal, decimal strength)
    {
        var context = signal.Context;

        if (context.IsEmpty || signal.Kind == SignalKind.Location)
        {
            return;
        }

        if (context.CountryCode is { } country)
        {
            profile.ObserveLocation(
                AffinityScope.Country, country, country, strength, signal.OccurredAt,
                AffinityId(AffinityScope.Country, country));
        }

        if (context.CityName is { } city)
        {
            profile.ObserveLocation(
                AffinityScope.City, city, city, strength, signal.OccurredAt,
                AffinityId(AffinityScope.City, city));
        }

        if (context.NeighborhoodName is { } neighborhood)
        {
            profile.ObserveLocation(
                AffinityScope.Neighborhood, neighborhood, neighborhood, strength, signal.OccurredAt,
                AffinityId(AffinityScope.Neighborhood, neighborhood));
        }
    }

    /// <summary>
    /// Re-applies everything the member stated, after every fold.
    /// <para>
    /// Runs at the end of both paths so a stated preference always wins over a derived one — a
    /// favourite location the member named is not something a week of browsing elsewhere should
    /// erode, and an explicit interest is not something a dismissal should pull down.
    /// </para>
    /// </summary>
    private void ApplyStatedPreferences(
        PersonalizationProfile profile, PersonalizationPreference? preference, DateTimeOffset now)
    {
        if (preference is null)
        {
            return;
        }

        if (preference.PreferredLanguage is { } language)
        {
            profile.SetLanguage(language);
        }

        foreach (var location in preference.FavoriteLocations)
        {
            profile.ObserveLocation(
                location.Scope,
                location.Key,
                location.DisplayName,
                1m,
                now,
                AffinityId(location.Scope, location.Key));
        }

        foreach (var category in preference.Categories.Where(entry => !entry.IsMuted))
        {
            profile.DeclareInterest(
                InterestKind.Category, category.CategoryKey, now, _guids.NewSequentialGuid());
        }
    }

    private Guid AffinityId(AffinityScope scope, string key)
    {
        _ = scope;
        _ = key;

        return _guids.NewSequentialGuid();
    }

    private static int ParseLevel(string? key) =>
        int.TryParse(key, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var level)
            ? level
            : 0;
}
