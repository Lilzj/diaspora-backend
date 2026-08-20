using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Extensibility.Signals;

/// <summary>
/// A signal somebody wants recorded, before the store has decided anything about it.
/// <para>
/// <b>The gap between this and <c>UserSignal</c> is where every policy about recording lives.</b> A
/// draft has no weight, no expiry and no identity; the store assigns all three, checks the member's
/// privacy preference, clamps what a client is allowed to claim, and only then writes a row. A
/// consumer that constructed the aggregate directly would bypass every one of those, which is why
/// nothing outside the store may.
/// </para>
/// </summary>
public sealed record SignalDraft
{
    public required UserId UserId { get; init; }

    public required SignalKind Kind { get; init; }

    public required SignalSource Source { get; init; }

    public RecommendationTargetType? TargetType { get; init; }

    public Guid? TargetId { get; init; }

    /// <summary>A category, tag, skill, language or search term.</summary>
    public string? Key { get; init; }

    public InterestKind? KeyKind { get; init; }

    /// <summary>
    /// An override for the kind's default weight.
    /// <para>
    /// Null is the normal case and means "use the default for this kind". A consumer sets it only
    /// when it knows something the kind does not — a completed learning path is a stronger
    /// <see cref="SignalKind.LearningCompleted"/> than a completed one-page guide.
    /// </para>
    /// </summary>
    public decimal? Weight { get; init; }

    public SignalContext? Context { get; init; }

    public RecommendationSurface? Surface { get; init; }

    /// <summary>The published contract's namespace-qualified name, or <c>client</c>.</summary>
    public string? SourceEventName { get; init; }

    /// <summary>When the fact happened. Defaults to the recording moment when a producer omits it.</summary>
    public DateTimeOffset? OccurredAt { get; init; }
}
