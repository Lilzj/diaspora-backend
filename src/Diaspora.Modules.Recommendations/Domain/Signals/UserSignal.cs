using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Signals;

/// <summary>
/// One observed fact about one member. Written once, never changed.
/// <para>
/// <b>Immutability is the whole contract of this aggregate, and it is what makes the rest of the
/// module honest.</b> A profile is a derived thing, and a derived thing is only trustworthy if the
/// record it derives from cannot be quietly edited to make yesterday's ranking look better. Every
/// personalization decision this platform makes can be reconstructed from these rows and the
/// catalogue, which is what "deterministic and explainable" means when somebody asks in six months
/// why a particular member saw a particular flat.
/// </para>
/// <para>
/// <b><see cref="ProcessedAt"/> is the one mutable field, and it is bookkeeping rather than
/// content.</b> It records that the incremental profile builder has already folded this signal in;
/// without it, every build would re-read a member's whole history and a member with two years of
/// signals would have the slowest feed on the platform. It changes nothing about what the signal
/// says. The aggregate exposes no other setter, and there is no method anywhere that alters the
/// kind, the target, the weight or the moment.
/// </para>
/// <para>
/// <b>Weight is stored on the row rather than looked up at ranking time.</b> The default weight of a
/// signal kind is a product decision that will be tuned, and re-weighting history when it is tuned
/// would silently rewrite what the platform believed about a member last month. A row carries the
/// weight it was recorded with; changing the defaults changes the future.
/// </para>
/// <para>
/// <b>Not soft-deletable, deliberately.</b> A signal is deleted for exactly two reasons — retention
/// expiry and a GDPR erasure request — and both mean the record must actually be gone. A soft
/// delete would leave a member's browsing history in the table behind a query filter, which is the
/// opposite of what an erasure request asks for.
/// </para>
/// </summary>
public sealed class UserSignal : AggregateRoot<UserSignalId>, IAuditableEntity
{
    private UserSignal(
        UserSignalId id,
        UserId userId,
        SignalKind kind,
        SignalSource source,
        decimal weight,
        DateTimeOffset occurredAt,
        DateTimeOffset recordedAt) : base(id)
    {
        UserId = userId;
        Kind = kind;
        Source = source;
        Weight = weight;
        OccurredAt = occurredAt;
        RecordedAt = recordedAt;
        Context = SignalContext.Empty();
    }

    private UserSignal() { }

    public UserId UserId { get; private set; } = default!;

    public SignalKind Kind { get; private set; }

    /// <summary>Provenance only. No strategy branches on it — see <see cref="SignalSource"/>.</summary>
    public SignalSource Source { get; private set; }

    /// <summary>What the signal was about, when it was about a thing. Null for a location or trust signal.</summary>
    public RecommendationTargetType? TargetType { get; private set; }

    public Guid? TargetId { get; private set; }

    /// <summary>
    /// The category, tag, skill, language or search term this signal carries.
    /// <para>
    /// Free-form because the vocabularies are somebody else's. Normalised to lower case so a tag
    /// published by Jobs and the same tag recorded by the client are one interest rather than two.
    /// </para>
    /// </summary>
    public string? Key { get; private set; }

    /// <summary>Which vocabulary <see cref="Key"/> is drawn from.</summary>
    public InterestKind? KeyKind { get; private set; }

    /// <summary>Between 0 and 1, frozen at recording time. See the class summary.</summary>
    public decimal Weight { get; private set; }

    /// <summary>Where and in what language it happened. Frozen — see <see cref="SignalContext"/>.</summary>
    public SignalContext Context { get; private set; } = default!;

    /// <summary>Which surface it happened on, when it happened on one.</summary>
    public RecommendationSurface? Surface { get; private set; }

    /// <summary>
    /// The published event that produced this, or <c>client</c>.
    /// <para>
    /// The namespace-qualified contract name, for the reason B13 records: Jobs and Marketplace both
    /// publish an <c>OfferAcceptedIntegrationEvent</c>, and a short name would attribute one to the
    /// other in every audit.
    /// </para>
    /// </summary>
    public string? SourceEventName { get; private set; }

    /// <summary>When the fact happened, as its producer reported it.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>When this module wrote it down. Later than <see cref="OccurredAt"/>, sometimes much later.</summary>
    public DateTimeOffset RecordedAt { get; private set; }

    /// <summary>When the retention sweep may delete it. Always set — see the class summary.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>When the incremental profile builder folded it in. Bookkeeping — see the class summary.</summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    public bool IsProcessed => ProcessedAt is not null;

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>
    /// Records an observation.
    /// <para>
    /// The only constructor. Every field a strategy will ever read is supplied here, because there
    /// is no second call that could add one later.
    /// </para>
    /// </summary>
    public static UserSignal Record(
        UserSignalId id,
        UserId userId,
        SignalKind kind,
        SignalSource source,
        decimal weight,
        DateTimeOffset occurredAt,
        DateTimeOffset recordedAt,
        TimeSpan retention,
        RecommendationTargetType? targetType = null,
        Guid? targetId = null,
        string? key = null,
        InterestKind? keyKind = null,
        SignalContext? context = null,
        RecommendationSurface? surface = null,
        string? sourceEventName = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);

        var signal = new UserSignal(
            id,
            userId,
            kind,
            source,
            Math.Clamp(weight, 0m, 1m),
            occurredAt,
            recordedAt)
        {
            TargetType = targetType,
            TargetId = targetId == Guid.Empty ? null : targetId,
            Key = Normalize(key),
            KeyKind = keyKind,
            Surface = surface,
            SourceEventName = string.IsNullOrWhiteSpace(sourceEventName) ? null : sourceEventName.Trim(),
            ExpiresAt = recordedAt.Add(retention)
        };

        if (context is not null)
        {
            signal.Context = context;
        }

        return signal;
    }

    /// <summary>
    /// Marks the signal as folded into the profile.
    /// <para>
    /// The only mutation this aggregate permits, and it says nothing about the fact itself. See the
    /// class summary for why the exception exists.
    /// </para>
    /// </summary>
    public void MarkProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt ??= processedAt;
    }

    /// <summary>
    /// How much this signal should count now, given how old it is.
    /// <para>
    /// <b>Half-life decay rather than a cliff.</b> A window — "signals from the last ninety days" —
    /// means a member's interests change discontinuously on the ninetieth night, and means a member
    /// who was away for a season comes back to a platform that has forgotten them entirely. An
    /// exponential decay degrades smoothly and never quite reaches zero, which is the right shape
    /// for "I still like the things I liked last year, just less".
    /// </para>
    /// </summary>
    public decimal EffectiveWeight(DateTimeOffset asOf, TimeSpan halfLife)
    {
        if (halfLife <= TimeSpan.Zero)
        {
            return Weight;
        }

        var age = asOf - OccurredAt;

        if (age <= TimeSpan.Zero)
        {
            return Weight;
        }

        var halfLives = age.TotalDays / halfLife.TotalDays;
        var factor = (decimal)Math.Pow(0.5, halfLives);

        return decimal.Round(Math.Clamp(Weight * factor, 0m, 1m), 6);
    }

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

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
