using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Events;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Preferences;
using Diaspora.Modules.Recommendations.Domain.Signals;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.Modules.Recommendations.Observability;
using Diaspora.Modules.Recommendations.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Recommendations.Infrastructure.Signals;

/// <summary>
/// The one door into the signal record, and therefore the one place privacy is enforced.
/// <para>
/// <b>Four things happen here that cannot happen anywhere else.</b> The member's
/// <c>ShareActivitySignals</c> preference is checked and behavioural drafts are dropped when it is
/// off; a client-supplied weight is clamped to what a client is allowed to claim; the retention
/// expiry every row is swept by is stamped; and the batch event is raised once for the batch rather
/// than once per row. A consumer that constructed <c>UserSignal</c> directly would skip all four,
/// silently, and no test would fail — which is why nothing else may.
/// </para>
/// <para>
/// <b>Which kinds survive an opt-out is a real distinction, not a loophole.</b> A member who turned
/// signal sharing off is refusing to have their behaviour recorded. They are not refusing to have a
/// country: the location, language, trust, verification and onboarding kinds are facts their own
/// modules already hold, and dropping them would break cold start for the members most likely to
/// have opted out without protecting anything, because Identity holds the country either way.
/// </para>
/// <para>
/// <b>The batch event carries a count and the distinct kinds, never the signals.</b> Publishing what
/// a member looked at on a bus every module reads would be the disclosure this module exists to
/// avoid making.
/// </para>
/// </summary>
public sealed class DefaultSignalStore : ISignalStore
{
    /// <summary>Kinds that survive a signal-sharing opt-out. See the class summary.</summary>
    private static readonly SignalKind[] NonBehavioural =
    [
        SignalKind.Location,
        SignalKind.Language,
        SignalKind.Trust,
        SignalKind.Verification,
        SignalKind.Onboarding,
        SignalKind.Preference
    ];

    private readonly ISignalRepository _signals;
    private readonly IPersonalizationPreferenceRepository _preferences;
    private readonly ISignalWeightPolicy _weights;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IOptions<RecommendationOptions> _options;

    public DefaultSignalStore(
        ISignalRepository signals,
        IPersonalizationPreferenceRepository preferences,
        ISignalWeightPolicy weights,
        IGuidGenerator guids,
        IClock clock,
        IOptions<RecommendationOptions> options)
    {
        _signals = signals;
        _preferences = preferences;
        _weights = weights;
        _guids = guids;
        _clock = clock;
        _options = options;
    }

    public async Task<bool> RecordAsync(
        SignalDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return await RecordManyAsync([draft], cancellationToken) > 0;
    }

    public async Task<int> RecordManyAsync(
        IReadOnlyList<SignalDraft> drafts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(drafts);

        if (drafts.Count == 0)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var retention = _options.Value.SignalRetention;

        // Preferences are read once per member in the batch rather than once per draft. A batch is
        // normally one member's impressions, so this is one read for twenty rows.
        var preferences = new Dictionary<Guid, PersonalizationPreference?>();
        var accepted = new List<UserSignal>(drafts.Count);
        var dropped = 0;

        foreach (var draft in drafts)
        {
            if (!preferences.TryGetValue(draft.UserId.Value, out var preference))
            {
                preference = await _preferences.FindAsync(draft.UserId, cancellationToken);
                preferences[draft.UserId.Value] = preference;
            }

            if (!IsAllowed(draft, preference))
            {
                dropped++;
                continue;
            }

            accepted.Add(Build(draft, now, retention));
        }

        if (dropped > 0)
        {
            RecommendationTelemetry.SignalsDropped.Add(dropped);
        }

        if (accepted.Count == 0)
        {
            return 0;
        }

        _signals.AddRange(accepted);

        RecommendationTelemetry.SignalsRecorded.Add(accepted.Count);

        AnnounceBatches(accepted, now);

        return accepted.Count;
    }

    public Task<IReadOnlyList<UserSignal>> ReadRecentAsync(
        UserId userId, int limit, CancellationToken cancellationToken = default) =>
        _signals.ListForUserAsync(userId, limit, cancellationToken);

    public Task<int> EraseAsync(UserId userId, CancellationToken cancellationToken = default) =>
        _signals.DeleteForUserAsync(userId, cancellationToken);

    /// <summary>
    /// Whether a draft survives the member's stated preferences.
    /// <para>
    /// The master switch stops everything behavioural; the narrower switch stops the same set. Both
    /// are checked because a member can disable signal sharing without disabling personalization —
    /// "use what I told you, do not watch me" is a coherent position and the two flags exist so it
    /// can be held.
    /// </para>
    /// </summary>
    private static bool IsAllowed(SignalDraft draft, PersonalizationPreference? preference)
    {
        if (preference is null)
        {
            return true;
        }

        if (NonBehavioural.Contains(draft.Kind))
        {
            return true;
        }

        return preference.IsPersonalizationEnabled && preference.ShareActivitySignals;
    }

    private UserSignal Build(SignalDraft draft, DateTimeOffset now, TimeSpan retention)
    {
        var defaultWeight = _weights.WeightFor(draft.Kind);
        var ceiling = _weights.CeilingFor(draft.Source);

        // A supplied weight is a request, never an instruction. See the class summary.
        var weight = Math.Min(draft.Weight ?? defaultWeight, ceiling);

        return UserSignal.Record(
            UserSignalId.From(_guids.NewSequentialGuid()),
            draft.UserId,
            draft.Kind,
            draft.Source,
            weight,
            draft.OccurredAt ?? now,
            now,
            retention,
            draft.TargetType,
            draft.TargetId,
            draft.Key,
            draft.KeyKind,
            draft.Context,
            draft.Surface,
            draft.SourceEventName);
    }

    /// <summary>
    /// Raises one event per member per source in the batch.
    /// <para>
    /// Per member because a batch can span members — the profile sweep records for many — and a
    /// consumer that received one event covering several people could not act on it. Per source
    /// because provenance is the point of the field.
    /// </para>
    /// </summary>
    private static void AnnounceBatches(IReadOnlyList<UserSignal> accepted, DateTimeOffset now)
    {
        var groups = accepted.GroupBy(signal => (signal.UserId.Value, signal.Source));

        foreach (var group in groups)
        {
            var carrier = group.First();

            carrier.AddDomainEvent(new SignalsRecordedDomainEvent
            {
                UserId = carrier.UserId,
                SignalCount = group.Count(),
                Kinds = group.Select(signal => signal.Kind).Distinct().Order().ToList(),
                Source = group.Key.Source,
                RecordedAt = now
            });
        }
    }
}
