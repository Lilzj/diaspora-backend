using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Guides.Extensibility.Ranking;

/// <summary>
/// What a ranking strategy knows about the request it is ordering results for.
/// <para>
/// <see cref="At"/> is handed in rather than read from a clock, so a strategy that weighs
/// recency is deterministic — the same candidates at the same instant rank the same way in a
/// test as in production.
/// </para>
/// </summary>
public sealed record GuideRankingContext
{
    /// <summary>The filter the candidates came from. Read-only context, not a re-query.</summary>
    public required GuideQueryFilter Filter { get; init; }

    /// <summary>Now, so a strategy can weigh recency without reading a clock of its own.</summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>The caller, when there is one.</summary>
    public UserId? ViewerId { get; init; }

    /// <summary>The free-text term, when one was supplied.</summary>
    public string? Query { get; init; }
}
