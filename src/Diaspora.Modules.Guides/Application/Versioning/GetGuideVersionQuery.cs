using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Versioning;

/// <summary>
/// One published version in full — what a reader was actually shown at that publication.
/// <para>
/// Addressed by version <em>number</em> rather than by row identifier, because the number is what
/// the history list shows, what a citation quotes and what an author asks to restore. The
/// content is immutable: a guide that once gave wrong advice cannot rewrite the record of having
/// given it.
/// </para>
/// </summary>
public sealed record GetGuideVersionQuery : IQuery<GuideVersionResponse>
{
    public required Guid GuideId { get; init; }

    /// <summary>One-based. Version one is the first publication.</summary>
    public required int VersionNumber { get; init; }
}
