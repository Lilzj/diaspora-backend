using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Suggestions;

/// <summary>
/// Proposes an improvement to somebody else's guide — the platform's lightweight merge request.
/// <para>
/// <b>Accepting one does not edit the guide.</b> The author applies the text through the
/// ordinary editor, where it gets the same validation, the same revision check and the same
/// version history as anything else they write. A suggestion that could write itself into a
/// published guide would be an edit permission granted to strangers by another name.
/// </para>
/// <para>
/// Authors do not suggest to their own guides — they edit them, and the aggregate says so.
/// </para>
/// </summary>
[Authorize(Permission = GuidePermissions.Suggest)]
[Auditable]
public sealed record SubmitGuideSuggestionCommand : ICommand<GuideSuggestionResponse>
{
    public required Guid GuideId { get; init; }

    public required SubmitGuideSuggestionRequest Request { get; init; }
}
