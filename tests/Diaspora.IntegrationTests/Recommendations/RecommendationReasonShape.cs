namespace Diaspora.IntegrationTests.Recommendations;

/// <summary>
/// Why a member is seeing something.
/// <para>
/// Three renderings, and the suite asserts all three are present — a client that met a code its
/// bundle predates falls back to the text, and a card with neither is the opaque recommendation the
/// module exists to prevent.
/// </para>
/// </summary>
public sealed record RecommendationReasonShape
{
    public string? Code { get; init; }

    public string? ResourceKey { get; init; }

    public string? Text { get; init; }

    public string? Confidence { get; init; }

    public List<string> SignalKinds { get; init; } = [];
}
