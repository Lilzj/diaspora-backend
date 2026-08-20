namespace Diaspora.Modules.Jobs.Extensibility.Search;

/// <summary>
/// What a search returned.
/// <para>
/// <see cref="TotalCount"/> is nullable because a genuine engine's total is an estimate and some
/// cannot produce one at all without a second pass. Null means "unknown", which a client renders
/// as "many" — and which is far better than a number that is confidently wrong.
/// </para>
/// </summary>
public sealed record JobSearchOutcome
{
    public required IReadOnlyList<JobSearchHit> Hits { get; init; }

    /// <summary>Opaque and engine-owned. Round-tripped into the next request.</summary>
    public string? NextCursor { get; init; }

    public bool HasMore { get; init; }

    /// <summary>An estimate, or null when the engine cannot say. Never rendered as exact.</summary>
    public long? TotalCount { get; init; }

    /// <summary>Nothing matched.</summary>
    public static JobSearchOutcome Empty { get; } =
        new() { Hits = [], NextCursor = null, HasMore = false, TotalCount = 0 };
}
