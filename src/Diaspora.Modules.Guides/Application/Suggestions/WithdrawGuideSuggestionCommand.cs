using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Suggestions;

/// <summary>
/// The suggester takes their proposal back.
/// <para>
/// Only they can. A withdrawal is a statement about the suggester's own intent, and letting the
/// guide's author "withdraw" a suggestion on their behalf would be a rejection wearing a
/// friendlier word — the aggregate refuses it, whoever asks.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.Suggest)]
[Auditable]
public sealed record WithdrawGuideSuggestionCommand : ICommand<GuideSuggestionResponse>
{
    public required Guid SuggestionId { get; init; }
}
