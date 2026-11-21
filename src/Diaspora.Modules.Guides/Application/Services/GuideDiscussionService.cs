using Diaspora.Modules.Community.Integration;
using Microsoft.Extensions.Logging;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// The discussion under a guide, read from Community.
/// <para>
/// B8 owns the knowledge and none of the conversation: threading, moderation, mentions and
/// tombstoning all come from B6, identically to every other surface, because this module
/// registered a content source and nothing more. This class is the one file that knows the
/// comments live there.
/// </para>
/// <para>
/// Reads fail soft. A guide whose count cannot be fetched renders without one — a knowledge
/// page taken down by a comments outage would be the tail wagging the dog.
/// </para>
/// <para>
/// <b>It reads the count and nothing else.</b> A <c>GetThreadAsync</c> here returned B6's
/// <c>CommentThread</c> — an aggregate — and had no caller: the thread itself is served to
/// clients by B6's own endpoint, and the only thing B8 ever needed in-process was the number
/// its engagement rollup is replaced from. It was removed on 21 November 2026 with the last
/// crossings into B6, and the shortest honest way to stop naming another module's aggregate
/// turned out to be not to call for one.
/// </para>
/// </summary>
public sealed class GuideDiscussionService
{
    private readonly ICommunityCommentService _comments;
    private readonly ILogger<GuideDiscussionService> _logger;

    public GuideDiscussionService(
        ICommunityCommentService comments, ILogger<GuideDiscussionService> logger)
    {
        _comments = comments;
        _logger = logger;
    }

    /// <summary>
    /// Community's authoritative comment count. The engagement rollup is refreshed from this
    /// — replaced, never incremented — because a recount cannot drift the way a delta stream
    /// can. Null when Community could not answer, so a caller never mistakes an outage for
    /// zero comments and writes the zero back.
    /// </summary>
    public async Task<int?> CountAsync(
        GuideId guideId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _comments.CountAsync(
                GuideBookmarkService.ReferenceFor(guideId), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Counting comments for guide {GuideId} failed.", guideId.Value);

            return null;
        }
#pragma warning restore CA1031
    }
}
