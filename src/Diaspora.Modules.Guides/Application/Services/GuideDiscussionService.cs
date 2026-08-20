using Diaspora.Modules.Community.Domain.Comments;
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
/// Reads fail soft. A guide whose discussion cannot be fetched renders with an empty thread
/// and a zero count — a knowledge page taken down by a comments outage would be the tail
/// wagging the dog.
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

    /// <summary>The assembled thread — pinned floats, deep replies flatten, orphans adopt.</summary>
    public async Task<CommentThread> GetThreadAsync(
        GuideId guideId,
        int rootLimit,
        bool includeHidden = false,
        CancellationToken cancellationToken = default)
    {
        var reference = GuideBookmarkService.ReferenceFor(guideId);

        try
        {
            return await _comments.GetThreadAsync(
                reference, rootLimit, includeHidden, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A comments outage renders an empty thread, not a failed guide page.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Reading the discussion for guide {GuideId} failed.", guideId.Value);

            return CommentThread.Assemble(reference, []);
        }
#pragma warning restore CA1031
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
