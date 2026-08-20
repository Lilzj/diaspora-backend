using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Neighborhoods;

/// <summary>
/// Insights about a place.
/// <para>
/// Approved only, unless the caller is a moderator asking for the queue or an author reading their
/// own. An unapproved insight is a sentence about a real residential area that nobody has checked,
/// and the default of every read path must be that it does not exist.
/// </para>
/// </summary>
public sealed record ListNeighborhoodInsightsQuery
    : IQuery<IReadOnlyList<NeighborhoodInsightResponse>>
{
    /// <summary>The Location area. Required unless the moderation queue is being read.</summary>
    public Guid? AreaId { get; init; }

    public string? Category { get; init; }

    /// <summary>The moderation queue. Requires the insight moderation permission.</summary>
    public bool PendingOnly { get; init; }

    /// <summary>The caller own submissions, whatever their status.</summary>
    public bool MineOnly { get; init; }

    /// <summary>Most attested first rather than newest. What a profile page renders.</summary>
    public bool ByEndorsement { get; init; } = true;

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
