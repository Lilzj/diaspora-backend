using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;

namespace Diaspora.Modules.Providers.Infrastructure.Community;

/// <summary>
/// Makes providers commentable, reactable, reportable and bookmarkable, through B6.
/// <para>
/// <b>This one class is the whole of what B7 pays to get Community's social surface.</b> The
/// brief tells B7 to consume Community for mentions, bookmarks, recommendations, reviews and
/// moderation rather than reimplementing them, and B6's contract freeze publishes exactly this
/// seam for exactly that purpose. Registering it means the client's <c>ReportModal</c> with
/// <c>contentType="Listing"</c> works, comments on a provider work, and the saved-collections
/// screen keeps one store — none of which required a line of social code here.
/// </para>
/// <para>
/// <c>CommunityContentType.Listing</c> is claimed rather than invented: B6 reserved the value,
/// with the comment "A service provider listing", before this module existed. Two modules
/// claiming one content type fails startup, which is why the claim is worth stating.
/// </para>
/// <para>
/// <b>Visibility is narrowed, never widened.</b> B6 takes the descriptor's audience as a ceiling,
/// so a provider that is unlisted or private must not be described as public — that would let a
/// comment on it surface in a feed that has no business showing it.
/// </para>
/// </summary>
public sealed class ProviderContentSource : ICommunityContentSource
{
    private readonly IProviderRepository _providers;

    public ProviderContentSource(IProviderRepository providers) => _providers = providers;

    public CommunityContentType ContentType => CommunityContentType.Listing;

    public async Task<ContentDescriptor?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        var provider = await _providers.GetForReadAsync(
            ProviderId.From(contentId), cancellationToken);

        return provider is null ? null : Describe(provider);
    }

    public async Task<IReadOnlyDictionary<Guid, ContentDescriptor>> DescribeManyAsync(
        IReadOnlyCollection<Guid> contentIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentIds);

        if (contentIds.Count == 0)
        {
            return new Dictionary<Guid, ContentDescriptor>();
        }

        var providers = await _providers.GetManyAsync(
            contentIds.Select(ProviderId.From).ToList(), cancellationToken);

        return providers.ToDictionary(provider => provider.Id.Value, Describe);
    }

    /// <summary>
    /// Applies a moderation outcome to the provider itself.
    /// <para>
    /// Optional on the interface, and implemented here because a moderator hiding a provider's
    /// content and leaving the provider itself in the directory is a half-measure that satisfies
    /// nobody. Community records the decision and publishes the event either way; this is B7
    /// acting on its own row, which only B7 can do.
    /// </para>
    /// </summary>
    public async Task ApplyModerationAsync(
        Guid contentId,
        Modules.Community.Domain.Enums.ModerationActionType action,
        CancellationToken cancellationToken = default)
    {
        var provider = await _providers.GetAsync(ProviderId.From(contentId), cancellationToken);

        if (provider is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        switch (action)
        {
            case Modules.Community.Domain.Enums.ModerationActionType.Hide:
            case Modules.Community.Domain.Enums.ModerationActionType.Suspend:
                provider.Suspend(now, "Suspended following moderation.");
                break;

            case Modules.Community.Domain.Enums.ModerationActionType.Restore:
            case Modules.Community.Domain.Enums.ModerationActionType.Approve:
                provider.Reinstate(now);
                break;

            case Modules.Community.Domain.Enums.ModerationActionType.Archive:
            case Modules.Community.Domain.Enums.ModerationActionType.Reject:
                // Refused by the aggregate while the provider is suspended (7 Nov 2026), and
                // deliberately left that way: an archived provider can be republished by its
                // owner, so archiving a suspended one would undo the suspension.
                provider.Archive(now, "Archived following moderation.");
                break;

            default:
                // Pin, Unpin and Escalate say nothing about a provider's lifecycle. Community
                // has already recorded them; there is nothing for B7 to do.
                break;
        }
    }

    private static ContentDescriptor Describe(Provider provider) => new()
    {
        Reference = ContentReference.Create(CommunityContentType.Listing, provider.Id.Value),
        AuthorId = provider.PrimaryOwnerId.Value,
        Title = provider.Name.Value,
        Exists = !provider.IsDeleted && provider.Status != ProviderStatus.Archived,
        Visibility = ToCommunityVisibility(provider),
        CountryCode = provider.Location.CountryCode,
        AreaId = provider.Location.AreaId,

        // A suspended or archived provider stops taking comments. A paused one does not: a
        // restaurant closed for August is still a business people have things to say about.
        AcceptsComments = provider.Status
            is ProviderStatus.Active or ProviderStatus.Paused or ProviderStatus.Closed
    };

    /// <summary>
    /// Projects B7's audience onto B6's, narrowing wherever the two do not line up.
    /// <para>
    /// B6's vocabulary has no <c>Unlisted</c> and no notion of an area-scoped audience, so both
    /// map to its narrowest sensible equivalent rather than to <c>Public</c>. Erring outward here
    /// would surface a private provider's comments in a public feed, which is a privacy failure
    /// rather than a rendering bug.
    /// </para>
    /// </summary>
    private static VisibilityLevel ToCommunityVisibility(Provider provider) =>
        provider.Visibility.Level switch
        {
            ProviderVisibilityLevel.Public => VisibilityLevel.Public,
            ProviderVisibilityLevel.Members => VisibilityLevel.Members,
            ProviderVisibilityLevel.CountryCommunity => VisibilityLevel.Country,
            ProviderVisibilityLevel.AreaOnly => VisibilityLevel.Neighborhood,
            _ => VisibilityLevel.Private
        };
}
