namespace Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;

/// <summary>
/// When a figure was computed and how far it should be trusted.
/// <para>
/// <b>Field-for-field the client's <c>DataFreshness</c>, and required on every figure this module
/// serves.</b> The frontend's own note is the reason it is shaped this way: "a type that cannot be
/// constructed without saying when it was computed and where it came from is how that stops being
/// a matter of remembering." That guarantee held on the client and evaporated at the wire, because
/// the certified route returned three bare numbers. This is the server keeping the same promise.
/// </para>
/// </summary>
public sealed record DataFreshnessResponse
{
    /// <summary>When the underlying data was computed or last reviewed.</summary>
    public required DateTimeOffset ComputedAt { get; init; }

    /// <summary>
    /// Who produced it. Never blank, and never a fiction.
    /// <para>
    /// "Diaspora neighbourhood profiles" for a moderated band, "Diaspora platform content" for a
    /// count of published listings. A source name that named a survey nobody ran would be the
    /// worst field on this record.
    /// </para>
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>One sentence on how the figure was produced.</summary>
    public string? Methodology { get; init; }

    /// <summary>
    /// What the figure cannot tell you. Rendered verbatim next to the score.
    /// <para>
    /// Populated by the server for every dimension it serves, so the client's mandatory
    /// limitations block finally has server text behind it instead of the adapter's.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> Limitations { get; init; } = [];

    /// <summary>After which the figure should be refetched or shown as historical.</summary>
    public DateTimeOffset? ValidUntil { get; init; }

    /// <summary>
    /// How many observations are behind it, where the notion applies.
    /// <para>
    /// For a ranked dimension this is the size of the comparison population, not the count being
    /// ranked. A score of n=3 is not a score, and the client is entitled to see the n.
    /// </para>
    /// </summary>
    public int? SampleSize { get; init; }
}
