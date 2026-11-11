using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Suggestions;

/// <summary>
/// Accepts or declines an open suggestion.
/// <para>
/// One command for both answers, because they are the same act with opposite signs and a client
/// that has to choose an endpoint by the answer is a client with two code paths where there is
/// one decision. The note is shown to the suggester either way — a rejection nobody explains is
/// how contributors stop contributing.
/// </para>
/// <para>
/// Accepting records a judgement; it does not touch the guide. Applying the text is an ordinary
/// edit the author makes afterwards.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.EditOwn)]
[Auditable]
public sealed record DecideGuideSuggestionCommand : ICommand<GuideSuggestionResponse>
{
    public required Guid SuggestionId { get; init; }

    public required DecideGuideSuggestionRequest Request { get; init; }
}
