using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Integration;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// Saving a guide, delegated to Community.
/// <para>
/// <b>B8 publishes the bookmark endpoints and owns none of the storage.</b> Community's
/// saved-collections screen shows guides beside providers, posts and everything after them,
/// from one store with one folder model — a guide bookmark table here would make that screen
/// join two stores and would strand guides outside Community's collections. The reference is
/// <c>CommunityContentType.Guide</c>, which B6 reserved for this module before it existed.
/// </para>
/// <para>
/// Community publishes no per-type "list my saved guides" surface — only save, unsave and the
/// batched membership filter — so the "bookmarked guides" listing is answered by filtering a
/// candidate page through <see cref="FilterSavedAsync"/> rather than by a query Community
/// does not offer.
/// </para>
/// </summary>
public sealed class GuideBookmarkService
{
    private readonly ICommunityBookmarkService _bookmarks;
    private readonly ILogger<GuideBookmarkService> _logger;

    public GuideBookmarkService(
        ICommunityBookmarkService bookmarks, ILogger<GuideBookmarkService> logger)
    {
        _bookmarks = bookmarks;
        _logger = logger;
    }

    /// <summary>The reference B6 stores for a guide. One place, so the two never diverge.</summary>
    public static ContentReference ReferenceFor(GuideId guideId)
    {
        ArgumentNullException.ThrowIfNull(guideId);

        return ContentReference.Create(CommunityContentType.Guide, guideId.Value);
    }

    public async Task<Result> SaveAsync(
        GuideId guideId, UserId userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _bookmarks.SaveAsync(ReferenceFor(guideId), userId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A Community outage becomes a failed save, not an unhandled 500.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Saving guide {GuideId} through Community failed.", guideId.Value);

            return Result.Failure(GuideErrors.BookmarkFailed);
        }
#pragma warning restore CA1031
    }

    public async Task<Result> RemoveAsync(
        GuideId guideId, UserId userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var removed = await _bookmarks.UnsaveAsync(
                ReferenceFor(guideId), userId, cancellationToken);

            // Removing a bookmark that is not there is the state the caller asked for, so the
            // route can keep the idempotence it advertises. Community reports not-found because
            // its own API is about the row; B8's is about the outcome, and a client that
            // retries a DELETE after a dropped response must not be told it failed.
            return removed.IsFailure && removed.Error.Type == ErrorType.NotFound
                ? Result.Success()
                : removed;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Removing guide {GuideId} through Community failed.", guideId.Value);

            return Result.Failure(GuideErrors.BookmarkFailed);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// How many members have the guide saved, as Community counts them. Null when Community
    /// could not answer, so a caller never mistakes an outage for nobody and writes the zero back.
    /// </summary>
    public async Task<int?> CountAsync(GuideId guideId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _bookmarks.CountAsync(ReferenceFor(guideId), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Counting saves for guide {GuideId} failed.", guideId.Value);

            return null;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Which of these guides the member has saved.
    /// <para>
    /// Batched, because a discovery page renders a bookmark icon on every card. B6 answers
    /// with its own composite keys, translated back to guide identifiers here so nothing above
    /// this class has to know what a <c>ContentReference.Key</c> looks like. Failing soft to an
    /// empty set is right: an unfilled icon is a cosmetic loss, and a 500 because Community
    /// hiccuped is not.
    /// </para>
    /// </summary>
    public async Task<IReadOnlySet<Guid>> FilterSavedAsync(
        UserId? userId,
        IReadOnlyCollection<GuideId> guideIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(guideIds);

        if (userId is null || guideIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        try
        {
            var references = guideIds.Select(ReferenceFor).ToList();

            var byKey = references.ToDictionary(
                reference => reference.Key, reference => reference.Id, StringComparer.Ordinal);

            var savedKeys = await _bookmarks.FilterSavedAsync(userId, references, cancellationToken);

            return savedKeys
                .Where(byKey.ContainsKey)
                .Select(key => byKey[key])
                .ToHashSet();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Reading saved guides from Community failed.");

            return new HashSet<Guid>();
        }
#pragma warning restore CA1031
    }
}
