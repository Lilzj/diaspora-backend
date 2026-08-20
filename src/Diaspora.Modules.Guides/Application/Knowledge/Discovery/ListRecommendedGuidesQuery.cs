using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.Discovery;

/// <summary>
/// What a curator says to read after one guide.
/// <para>
/// The panel at the end of a guide. Curated, not inferred: "read this before you sign anything" is a
/// sentence a person wrote because they watched somebody get caught out.
/// </para>
/// </summary>
public sealed record ListRecommendedGuidesQuery : IQuery<IReadOnlyList<RecommendedReadResponse>>
{
    /// <summary>The guide somebody has just finished.</summary>
    public required Guid GuideId { get; init; }

    public int? Limit { get; init; }
}
