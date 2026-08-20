using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Suggestions;

namespace Diaspora.Modules.Guides.Application.Mapping;

/// <summary>
/// Turns a suggestion into the wire shape the author's review queue and the suggester's own
/// history render.
/// <para>
/// The decision note travels to both sides deliberately: a suggester told only "rejected"
/// learns nothing and suggests the same thing again, and an author who wrote a reason should
/// have it reach the person it was written for.
/// </para>
/// <para>
/// Static throughout — no state, only translation (the B6.0.1 CA1822 discipline: static from
/// the start rather than after the Release build says so).
/// </para>
/// </summary>
public static class GuideSuggestionMapper
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
