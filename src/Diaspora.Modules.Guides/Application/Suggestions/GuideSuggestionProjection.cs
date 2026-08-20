using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Suggestions;

namespace Diaspora.Modules.Guides.Application.Suggestions;

/// <summary>
/// Turns a suggestion aggregate into the shape the wire reads.
/// <para>
/// One definition, because every command in this folder answers with the same record and three
/// hand-written copies of a flat projection is three places for a field to be forgotten. Static
/// and stateless: translation only, never a fetch — a mapper that can fetch is a mapper nobody
/// can safely call from a loop.
/// </para>
/// </summary>
internal static class GuideSuggestionProjection
{
    public static GuideSuggestionResponse ToResponse(GuideSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        return new GuideSuggestionResponse
        {
            SuggestionId = suggestion.Id.Value,
            GuideId = suggestion.GuideId.Value,
            AuthorId = suggestion.AuthorId.Value,
            TargetSectionId = suggestion.TargetSectionId,
            ProposedContent = suggestion.ProposedContent,
            Rationale = suggestion.Rationale,
            Status = GuideWireNames.ToWire(suggestion.Status),
            DecidedBy = suggestion.DecidedBy?.Value,
            DecidedAt = suggestion.DecidedAt,
            DecisionNote = suggestion.DecisionNote,
            CreatedAt = suggestion.CreatedAt
        };
    }
}
