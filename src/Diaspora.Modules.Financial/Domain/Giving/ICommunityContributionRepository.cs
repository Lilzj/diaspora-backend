using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Giving;

/// <summary>Retrieval and staging for community contributions.</summary>
public interface ICommunityContributionRepository
{
    void Add(CommunityContribution contribution);

    void Update(CommunityContribution contribution);

    Task<CommunityContribution?> FindAsync(
        CommunityContributionId contributionId, CancellationToken cancellationToken = default);

    Task<CommunityContribution?> FindByReceiptAsync(
        string receiptReference, CancellationToken cancellationToken = default);

    Task<CommunityContribution?> FindByPaymentIntentAsync(
        PaymentIntentId paymentIntentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommunityContribution>> ListByContributorAsync(
        Guid contributorUserId, int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommunityContribution>> ListByFundAsync(
        ContributionFundId fundId,
        GivingStatus? status,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many completed contributions were not money, in a window.
    /// <para>
    /// Counted separately and never totalled with the money, because goods, hours and skills have no
    /// amount — and a giving summary that reported only cash would understate what the community
    /// actually did by a wide margin.
    /// </para>
    /// </summary>
    Task<int> CountNonMonetaryAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>The reconciliation input for a fund's running totals.</summary>
    Task<(long RaisedMinorUnits, int ContributorCount)> TotalsForFundAsync(
        ContributionFundId fundId, CancellationToken cancellationToken = default);
}
