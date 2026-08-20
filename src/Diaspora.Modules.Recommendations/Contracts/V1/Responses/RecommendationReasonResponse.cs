namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// Why a member is being shown something, on the wire.
/// <para>
/// <b>Three renderings, and the client is expected to prefer the first it can use.</b>
/// <see cref="ResourceKey"/> is what a translation bundle is keyed on and is what a shipped client
/// should render. <see cref="Text"/> is English and exists for the case a client meets a code its
/// bundle predates — showing a blank line where the reason should be is worse than showing English.
/// <see cref="Code"/> is what analytics group by and what a client switches layout on.
/// </para>
/// <para>
/// Never optional. A recommendation without a reason cannot be constructed on the server, so this
/// property is present on every card the API returns.
/// </para>
/// </summary>
public sealed record RecommendationReasonResponse
{
    /// <summary>The stable reason code — <c>LivesInCountry</c>, <c>ViewedSimilar</c>, and so on.</summary>
    public required string Code { get; init; }

    /// <summary>The translation key, in <c>recommendations.reason.*</c> form.</summary>
    public required string ResourceKey { get; init; }

    /// <summary>English, already substituted. The fallback — see the class summary.</summary>
    public required string Text { get; init; }

    /// <summary><c>Low</c>, <c>Medium</c> or <c>High</c>. Deterministic, derived from corroboration.</summary>
    public required string Confidence { get; init; }

    /// <summary>The kinds of observation this reason was derived from.</summary>
    public required IReadOnlyList<string> SignalKinds { get; init; }
}
