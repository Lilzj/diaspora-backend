using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.ValueObjects;

/// <summary>
/// Why one member is being shown one thing.
/// <para>
/// <b>Part of the contract, not decoration.</b> The brief is explicit that the frontend must never
/// receive an opaque recommendation, and that is enforced structurally: a
/// <c>RecommendationItem</c> cannot be constructed without one of these, so there is no code path
/// through this module that produces an unexplained card.
/// </para>
/// <para>
/// <b>Three renderings of one reason, and each has a different consumer.</b> The
/// <see cref="ReasonCode"/> is what analytics group by and what a client switches on. The
/// <see cref="ResourceKey"/> is what a translation table is keyed on. The
/// <see cref="FallbackText"/> is English, and exists for the case a client meets a code its
/// translation bundle predates — showing a blank card or a raw enum name in the one place the
/// product is asking to be trusted is worse than showing English.
/// </para>
/// <para>
/// <see cref="SignalKinds"/> is stored as a sorted comma-separated list rather than an owned
/// collection. It is read as a whole, written once, never queried by element, and never longer than
/// a handful of names — a child table for it would be three joins bought for nothing.
/// </para>
/// </summary>
public sealed class RecommendationExplanation : ValueObject
{
    private RecommendationExplanation(
        RecommendationReasonCode reasonCode,
        string resourceKey,
        string fallbackText,
        RecommendationConfidence confidence,
        string signalKinds)
    {
        ReasonCode = reasonCode;
        ResourceKey = resourceKey;
        FallbackText = fallbackText;
        Confidence = confidence;
        SignalKinds = signalKinds;
    }

    private RecommendationExplanation() { }

    public RecommendationReasonCode ReasonCode { get; private set; }

    /// <summary>The translation key, in <c>recommendations.reason.*</c> form.</summary>
    public string ResourceKey { get; private set; } = string.Empty;

    /// <summary>English, for a client whose bundle predates the code. See the class summary.</summary>
    public string FallbackText { get; private set; } = string.Empty;

    public RecommendationConfidence Confidence { get; private set; }

    /// <summary>The signal kinds this reason was derived from, sorted and comma-separated.</summary>
    public string SignalKinds { get; private set; } = string.Empty;

    public static RecommendationExplanation Create(
        RecommendationReasonCode reasonCode,
        string resourceKey,
        string fallbackText,
        RecommendationConfidence confidence,
        IEnumerable<SignalKind>? signalKinds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackText);

        var kinds = (signalKinds ?? [])
            .Distinct()
            .Order()
            .Select(kind => kind.ToString())
            .ToArray();

        return new RecommendationExplanation(
            reasonCode,
            resourceKey.Trim(),
            fallbackText.Trim(),
            confidence,
            string.Join(',', kinds));
    }

    /// <summary>The signal kinds behind the reason, parsed back out.</summary>
    public IReadOnlyList<SignalKind> ParseSignalKinds() =>
        SignalKinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => Enum.TryParse<SignalKind>(name, out var kind) ? (SignalKind?)kind : null)
            .Where(kind => kind is not null)
            .Select(kind => kind!.Value)
            .ToList();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ReasonCode;
        yield return ResourceKey;
        yield return FallbackText;
        yield return Confidence;
        yield return SignalKinds;
    }
}
