using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;

namespace Diaspora.Modules.Guides.Application.Knowledge.Curation;

/// <summary>
/// Sets what a curator says to read after one guide.
/// <para>
/// <b>The command the brief calls "Manage Recommended Reads", and it is editor-managed by
/// design.</b> Nothing infers an ordering, learns from what readers did next or scores a pair of
/// guides for similarity. "Read this before you sign anything" is a sentence a person wrote because
/// they watched somebody get caught out, and no amount of co-occurrence data produces it.
/// </para>
/// <para>
/// When an inferred version arrives it populates the same rows behind the same aggregate, and
/// nothing above it changes — which is most of why the curated version is worth building first.
/// </para>
/// </summary>
[Authorize(Permission = KnowledgePermissions.Curate)]
[Auditable]
public sealed record SetRecommendedReadsCommand : ICommand
{
    /// <summary>The guide somebody has just finished reading.</summary>
    public required Guid GuideId { get; init; }

    public required SetRecommendedReadsRequest Request { get; init; }
}
