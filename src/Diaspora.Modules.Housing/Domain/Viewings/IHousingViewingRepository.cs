using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Housing.Domain.Viewings;

/// <summary>How the module reaches its viewings.</summary>
public interface IHousingViewingRepository
{
    Task<HousingViewing?> GetAsync(
        HousingViewingId id, CancellationToken cancellationToken = default);

    Task<HousingViewing?> GetForReadAsync(
        HousingViewingId id, CancellationToken cancellationToken = default);

    /// <summary>The member's live viewing on a property, which is what stops a second one.</summary>
    Task<HousingViewing?> GetOpenForSeekerAsync(
        HousingListingId listingId,
        UserId seekerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether this member holds a viewing that entitles them to the property's full address.
    /// <para>
    /// The single most-called question in the module's read path: every listing detail render for
    /// a signed-in member asks it, which is why it is a boolean query against an index rather than
    /// a load of the viewing.
    /// </para>
    /// </summary>
    Task<bool> HasAddressUnlockingViewingAsync(
        HousingListingId listingId,
        UserId seekerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Whether this member has completed a viewing, for the reservation gate.</summary>
    Task<bool> HasCompletedViewingAsync(
        HousingListingId listingId,
        UserId seekerUserId,
        CancellationToken cancellationToken = default);

    Task<CursorPagedResult<HousingViewing>> QueryAsync(
        ViewingQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many viewings match, without loading a page of them. What the landlord dashboard counts
    /// its unanswered requests with.
    /// </summary>
    Task<int> CountAsync(
        ViewingQueryFilter filter, CancellationToken cancellationToken = default);

    Task<int> CountOpenBySeekerAsync(
        UserId seekerUserId, CancellationToken cancellationToken = default);

    /// <summary>Viewings whose slot has passed with nothing recorded. The sweep's work list.</summary>
    Task<IReadOnlyList<HousingViewing>> GetLapsedAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(HousingViewing viewing, CancellationToken cancellationToken = default);
}
