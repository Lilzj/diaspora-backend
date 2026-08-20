using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Housing.Domain.Neighborhoods;

/// <summary>
/// How an insight list is narrowed.
/// <para>
/// <b><see cref="IncludeUnapproved"/> defaults to false and only the moderation queue and an
/// author's own view ever set it.</b> An unapproved insight is a sentence about a real place that
/// nobody has checked, and the default of every read path must be that it does not exist.
/// </para>
/// </summary>
public sealed record NeighborhoodInsightQueryFilter
{
    public NeighborhoodProfileId? ProfileId { get; init; }

    /// <summary>B4's area, which is how a neighbourhood page asks.</summary>
    public Guid? AreaId { get; init; }

    public UserId? AuthorId { get; init; }

    public IReadOnlyCollection<NeighborhoodInsightCategory>? Categories { get; init; }

    public IReadOnlyCollection<NeighborhoodInsightStatus>? Statuses { get; init; }

    /// <summary>Set only by the moderation queue and by an author reading their own. See the summary.</summary>
    public bool IncludeUnapproved { get; init; }

    /// <summary>Most-endorsed first rather than newest. What a profile page renders.</summary>
    public bool OrderByEndorsement { get; init; }

    public int Limit { get; init; } = HousingLimits.DefaultPageSize;

    public int EffectiveLimit => Math.Clamp(Limit, 1, HousingLimits.MaxPageSize);
}
