using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Domain.Suggestions;

/// <summary>Reads and writes improvement suggestions.</summary>
public interface IGuideSuggestionRepository
{
    Task<GuideSuggestion?> GetAsync(
        GuideSuggestionId id, CancellationToken cancellationToken = default);

    /// <summary>A guide's suggestions, newest first, optionally narrowed to one status.</summary>
    Task<CursorPagedResult<GuideSuggestion>> ListForGuideAsync(
        GuideId guideId,
        GuideSuggestionStatus? status,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Every open suggestion, oldest first. The moderation queue's feed.</summary>
    Task<CursorPagedResult<GuideSuggestion>> ListOpenAsync(
        string? cursor, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(GuideSuggestion suggestion, CancellationToken cancellationToken = default);
}
