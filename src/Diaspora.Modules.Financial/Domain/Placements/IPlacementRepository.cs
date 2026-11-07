using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Domain.Placements;

/// <summary>Retrieval and staging for purchased placements.</summary>
public interface IPlacementRepository
{
    void Add(Placement placement);

    void Update(Placement placement);

    Task<Placement?> FindAsync(PlacementId placementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every placement currently in force over a subject.
    /// <para>
    /// The question every consuming surface actually asks, and the reason this module needs no
    /// knowledge of what a listing is: a caller passes a type and an identifier and gets back what
    /// has been paid for.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Placement>> ListActiveForSubjectAsync(
        SubjectReference subject, DateTimeOffset asOf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every placement over a subject that is running or waiting to run: what a moderation decision
    /// that takes the subject out of view pauses.
    /// </summary>
    Task<IReadOnlyList<Placement>> ListRunningOrScheduledForSubjectAsync(
        SubjectReference subject, CancellationToken cancellationToken = default);

    /// <summary>Every placement over a subject that a moderation decision paused.</summary>
    Task<IReadOnlyList<Placement>> ListPausedForModerationAsync(
        SubjectReference subject, CancellationToken cancellationToken = default);

    /// <summary>Active placements over many subjects at once, so a list page needs one query.</summary>
    Task<IReadOnlyList<Placement>> ListActiveForSubjectsAsync(
        MonetizationSubjectType subjectType,
        IReadOnlyCollection<Guid> subjectIds,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Placement>> ListByPurchaserAsync(
        Guid userId,
        PlacementStatus? status,
        DateTimeOffset? before,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many placements are in force right now, across everybody.
    /// <para>
    /// A count rather than a page, because the dashboard wants a number and loading every active
    /// placement to length it would get slower exactly as the platform succeeded.
    /// </para>
    /// </summary>
    Task<int> CountActiveAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default);

    /// <summary>Placements whose window has closed and which nothing has ended yet.</summary>
    Task<IReadOnlyList<Placement>> ListLapsedAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default);

    /// <summary>Scheduled placements whose window has opened.</summary>
    Task<IReadOnlyList<Placement>> ListDueAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Placements whose window closes within a lead time and which have not been announced.
    /// <para>
    /// The filter on <c>ExpiryAnnouncedFor</c> is in the query rather than in the sweep so a
    /// deployment with a hundred thousand placements does not load the ones it has already warned
    /// about on every pass.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Placement>> ListExpiringAsync(
        DateTimeOffset asOf,
        DateTimeOffset until,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a subject already has a placement of a kind overlapping a window.
    /// <para>
    /// Asked before a renewal extends one, because extending past another placement's start would
    /// produce the overlap the purchase path refuses.
    /// </para>
    /// </summary>
    Task<bool> HasOverlapAsync(
        SubjectReference subject,
        PlacementKind kind,
        DateWindow window,
        PlacementId? excluding,
        CancellationToken cancellationToken = default);
}
