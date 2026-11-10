using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Localization;

/// <summary>
/// Starts a translation of an existing guide.
/// <para>
/// A translation is a <em>separate guide</em> that happens to share a translation group, not a
/// second column on the original. That is what lets it be published, versioned, verified and
/// moderated on its own timeline — a Portuguese translation of a UK visa guide is somebody's
/// work, with its own accuracy and its own history, and burying it inside the English guide
/// would make it nobody's.
/// </para>
/// <para>
/// <c>[Transactional]</c> because the new draft, its link into the source's translation group and
/// its seeded structure are one act: a translation created without the group link is an orphan
/// nobody can navigate to from the original, and a link with no content is a promise the reader
/// finds empty.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.Create)]
[Transactional(Reason = "The translation draft, its group link and its seeded structure must commit together.")]
[Auditable]
public sealed record CreateGuideTranslationCommand : ICommand<GuideResponse>
{
    /// <summary>The guide being translated. Its translation group is the one joined.</summary>
    public required Guid GuideId { get; init; }

    public required CreateGuideTranslationRequest Request { get; init; }
}
