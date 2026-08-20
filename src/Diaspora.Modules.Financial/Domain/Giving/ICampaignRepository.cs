using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Giving;

/// <summary>What a campaign search asks for. A record so a new filter is one property, not one overload.</summary>
public sealed record CampaignFilter
{
    public CampaignStatus? Status { get; init; }

    public CampaignCategory? Category { get; init; }

    public string? CountryCode { get; init; }

    public Guid? OrganizationId { get; init; }

    /// <summary>
    /// When set, drafts and pending campaigns belonging to this member are included.
    /// <para>
    /// <b>The one place a non-public campaign becomes visible, and it is keyed by the viewer.</b> A
    /// filter that simply returned every status would list somebody's unpublished draft on a browse
    /// screen.
    /// </para>
    /// </summary>
    public Guid? ViewerUserId { get; init; }

    /// <summary>Restricts to campaigns the viewer organizes. The "my campaigns" read.</summary>
    public bool OrganizedByViewerOnly { get; init; }

    public DateTimeOffset? Before { get; init; }

    public IReadOnlyList<Guid> ExcludeIds { get; init; } = [];

    public int Take { get; init; } = 20;
}

/// <summary>Retrieval and staging for campaigns.</summary>
public interface ICampaignRepository
{
    void Add(Campaign campaign);

    void Update(Campaign campaign);

    Task<Campaign?> FindAsync(CampaignId campaignId, CancellationToken cancellationToken = default);

    /// <summary>The idempotency check. A retried creation replays rather than creating a second.</summary>
    Task<Campaign?> FindByIdempotencyKeyAsync(
        Guid organizerUserId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>The link back from a cause to the campaign that created it.</summary>
    Task<Campaign?> FindByCauseCodeAsync(
        string causeCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Campaign>> SearchAsync(
        CampaignFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Published campaigns whose deadline has passed and which nothing has moved on yet. The sweep's
    /// input.
    /// </summary>
    Task<IReadOnlyList<Campaign>> ListPastDeadlineAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every collecting campaign with at least one milestone still unreached. The milestone sweep's
    /// input.
    /// </summary>
    Task<IReadOnlyList<Campaign>> ListWithOpenMilestonesAsync(
        int take, CancellationToken cancellationToken = default);
}

/// <summary>Retrieval and staging for campaign updates.</summary>
public interface ICampaignUpdateRepository
{
    void Add(CampaignUpdate update);

    void Update(CampaignUpdate update);

    Task<CampaignUpdate?> FindAsync(
        CampaignUpdateId updateId, CancellationToken cancellationToken = default);

    Task<CampaignUpdate?> FindByIdempotencyKeyAsync(
        Guid authorUserId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// One campaign's timeline, newest first, cursor-paged.
    /// <para>
    /// <c>includeWithdrawn</c> is the organizer's own view: a supporter sees what stands, and the
    /// person who wrote it sees what they took down as well.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<CampaignUpdate>> ListByCampaignAsync(
        CampaignId campaignId,
        bool includeWithdrawn,
        DateTimeOffset? before,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken cancellationToken = default);
}

/// <summary>Retrieval and staging for recurring donation schedules.</summary>
public interface IRecurringDonationRepository
{
    void Add(RecurringDonation schedule);

    void Update(RecurringDonation schedule);

    Task<RecurringDonation?> FindAsync(
        RecurringDonationId scheduleId, CancellationToken cancellationToken = default);

    Task<RecurringDonation?> FindByIdempotencyKeyAsync(
        Guid donorUserId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>A donor's own schedules, newest first. Every state, so a cancelled one still shows.</summary>
    Task<IReadOnlyList<RecurringDonation>> ListByDonorAsync(
        Guid donorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules whose next charge has come round. The worker's input.
    /// <para>
    /// Ordered by the due date so the longest-overdue is taken first, and bounded, because a sweep
    /// that loaded every due schedule on a busy morning would hold them all in one change tracker.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<RecurringDonation>> ListDueAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default);
}

/// <summary>Retrieval and staging for the membership payment binding.</summary>
public interface IMembershipPaymentBindingRepository
{
    void Add(Memberships.MembershipPaymentBinding binding);

    void Update(Memberships.MembershipPaymentBinding binding);

    /// <summary>
    /// Forgets a staged binding the database refused.
    /// <para>
    /// <b>Without this, losing a concurrency race turns into a 500 one call later.</b> A unique-index
    /// violation leaves the rejected row still tracked as <c>Added</c>; the next
    /// <c>SaveChanges</c> in the same scope — and there is one, because the caller goes on to
    /// activate the membership — retries the same insert and hits the same index. The loser of the
    /// race is entitled to the same answer as the winner, and this is what lets it have one.
    /// </para>
    /// </summary>
    void Forget(Memberships.MembershipPaymentBinding binding);

    /// <summary>The duplicate check, and the answer a losing concurrent request is given.</summary>
    Task<Memberships.MembershipPaymentBinding?> FindByPaymentIntentAsync(
        PaymentIntentId paymentIntentId, CancellationToken cancellationToken = default);

    /// <summary>Every payment bound to one membership, newest first. Renewals mean there can be several.</summary>
    Task<IReadOnlyList<Memberships.MembershipPaymentBinding>> ListByMembershipAsync(
        Guid membershipId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bindings whose activation may never have landed. The reconciliation sweep's input.
    /// <para>
    /// Bounded by <paramref name="maxReannouncements"/> so a membership nothing can activate — a
    /// deleted organization, a member who left — is not re-announced for ever.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Memberships.MembershipPaymentBinding>> ListForReconciliationAsync(
        DateTimeOffset boundBefore,
        int maxReannouncements,
        int take,
        CancellationToken cancellationToken = default);
}
