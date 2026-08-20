using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Integration;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// Saving a provider, delegated to Community.
/// <para>
/// <b>B7 publishes the bookmark endpoints and owns none of the storage.</b> The brief lists
/// <c>ProviderBookmark</c> as a domain aggregate and, three sections later, forbids duplicating
/// Community's bookmarks. This class is the resolution: the API surface the brief asks for,
/// implemented against <c>ICommunityBookmarkService</c> with a
/// <c>ContentReference(Listing, providerId)</c>.
/// </para>
/// <para>
/// The client settles the argument. Its <c>SavedContentType</c> already treats a saved provider
/// as one entry — <c>'Listing'</c>, labelled "Providers" — in a single saved-collections screen
/// alongside guides, jobs, housing and events. A second bookmark table would make that screen
/// join two stores, and the folders and collections B6 already ships would be missing for
/// providers alone. Recorded as deviation D-2 in the frontend contract report.
/// </para>
/// <para>
/// <c>CommunityContentType.Listing</c> is not a value B7 invented: B6 reserved it, with the
/// comment "A service provider listing", before this module existed.
/// </para>
/// </summary>
public sealed class ProviderBookmarkService
{
    private readonly ICommunityBookmarkService _bookmarks;
    private readonly ILogger<ProviderBookmarkService> _logger;

    public ProviderBookmarkService(
        ICommunityBookmarkService bookmarks, ILogger<ProviderBookmarkService> logger)
    {
        _bookmarks = bookmarks;
        _logger = logger;
    }

    /// <summary>The reference B6 stores for a provider. One place, so the two never diverge.</summary>
    public static ContentReference ReferenceFor(ProviderId providerId)
    {
        ArgumentNullException.ThrowIfNull(providerId);

        return ContentReference.Create(CommunityContentType.Listing, providerId.Value);
    }

    public async Task<Result> SaveAsync(
        ProviderId providerId, UserId userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _bookmarks.SaveAsync(ReferenceFor(providerId), userId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A Community outage becomes a failed save, not an unhandled 500.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Saving provider {ProviderId} through Community failed.", providerId.Value);

            return Result.Failure(ProviderErrors.BookmarkFailed);
        }
#pragma warning restore CA1031
    }

    public async Task<Result> RemoveAsync(
        ProviderId providerId, UserId userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _bookmarks.UnsaveAsync(ReferenceFor(providerId), userId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Removing provider {ProviderId} through Community failed.", providerId.Value);

            return Result.Failure(ProviderErrors.BookmarkFailed);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Which of these providers the member has saved.
    /// <para>
    /// Batched, because a discovery page renders a heart on every card. B6 answers with its own
    /// composite keys, which are translated back to provider identifiers here so nothing above
    /// this class has to know what a <c>ContentReference.Key</c> looks like.
    /// </para>
    /// <para>
    /// Failing soft to an empty set is right: an unfilled heart is a cosmetic loss, and a 500
    /// because Community hiccuped is not.
    /// </para>
    /// </summary>
    public async Task<IReadOnlySet<Guid>> FilterSavedAsync(
        UserId? userId,
        IReadOnlyCollection<ProviderId> providerIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerIds);

        if (userId is null || providerIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        try
        {
            var references = providerIds.Select(ReferenceFor).ToList();

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
            _logger.LogWarning(exception, "Reading saved providers from Community failed.");

            return new HashSet<Guid>();
        }
#pragma warning restore CA1031
    }
}
