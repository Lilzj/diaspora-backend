using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Earnings;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Creator.Application.Consumers;

/// <summary>
/// Turns a payment the platform already published into an earning with a meaning.
/// <para>
/// <b>Every consumer in this file is a thin call into <see cref="RecordAsync"/>.</b> DSP1002 reports
/// two consumers settling one event, and the risk here is the subtler one: two published contracts
/// both mean "money arrived and somebody earned from it", and two copies of the rate arithmetic,
/// the idempotency check and the creator lookup would drift.
/// </para>
/// <para>
/// <b>Nothing here moves money.</b> An earning is a claim — this much, from this, in this state.
/// The wallet is B15's and moves when a payout captures. A consumer that credited a balance would be
/// a second payment path, which §18 forbids for exactly the reason it forbids one for advertising.
/// </para>
/// </summary>
public sealed class CreatorEarningRecorder
{
    private readonly ICreatorProfileRepository _creators;
    private readonly ICreatorEarningRepository _earnings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CreatorRevenueShareOptions _shares;

    public CreatorEarningRecorder(
        ICreatorProfileRepository creators,
        ICreatorEarningRepository earnings,
        IUnitOfWork unitOfWork,
        IOptions<CreatorRevenueShareOptions> shares)
    {
        ArgumentNullException.ThrowIfNull(shares);

        _creators = creators;
        _earnings = earnings;
        _unitOfWork = unitOfWork;
        _shares = shares.Value;
    }

    public async Task RecordAsync(
        Guid userId,
        CreatorEarningSource source,
        long grossMinorUnits,
        string currency,
        int exponent,
        string sourceReference,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken,
        string? subjectType = null,
        Guid? subjectId = null)
    {
        if (userId == Guid.Empty || grossMinorUnits <= 0)
        {
            return;
        }

        // The idempotency barrier, checked before anything is loaded. Delivery is at-least-once, and
        // the unique index behind this is what settles two workers racing one delivery — this check
        // is the cheap path, not the guarantee.
        if (await _earnings.ExistsAsync(sourceReference, cancellationToken))
        {
            return;
        }

        // **A creator profile is not opened here**, unlike the activity ledger, which opens one for
        // anybody who does something the programme recognises. Receiving money does not make
        // somebody a creator, and a profile conjured by a donation would put a member who has never
        // published anything into the creator directory.
        var creator = await _creators.FindByUserAsync(userId, cancellationToken);

        if (creator is null)
        {
            return;
        }

        var share = _shares.ShareOf(grossMinorUnits, source);

        var earning = CreatorEarning.Record(
            creator.Id,
            userId,
            source,
            share,
            grossMinorUnits,
            currency,
            exponent,
            sourceReference,
            subjectType,
            subjectId,
            occurredAt);

        // A refusal here is a configuration fault — a rate above 100%, a missing currency — and
        // dropping it silently would leave the dashboard short of an earning nobody can account for.
        // The aggregate's own errors carry the reason; this returns rather than throwing, because an
        // integration handler that throws re-delivers for ever on a message it can never accept.
        if (earning.IsFailure)
        {
            return;
        }

        _earnings.Add(earning.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// A donation whose beneficiary is a member.
/// <para>
/// <b>Only <c>Member</c>.</b> `BeneficiaryType` also carries Organization, Community, Fund and
/// Platform, and none of those is a creator's earning — money to a community fund belongs to the
/// community. Attributing it to whoever administers the fund would be inventing a revenue model out
/// of an administrative relationship.
/// </para>
/// </summary>
public sealed class DonationCompletedEarningConsumer
    : IntegrationEventHandler<DonationCompletedIntegrationEvent>
{
    private readonly CreatorEarningRecorder _recorder;

    public DonationCompletedEarningConsumer(CreatorEarningRecorder recorder) => _recorder = recorder;

    protected override Task HandleAsync(
        DonationCompletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!string.Equals(integrationEvent.BeneficiaryType, "Member", StringComparison.OrdinalIgnoreCase)
            || integrationEvent.BeneficiaryId is not { } beneficiaryId)
        {
            return Task.CompletedTask;
        }

        return _recorder.RecordAsync(
            beneficiaryId,
            CreatorEarningSource.Donation,
            integrationEvent.AmountMinorUnits,
            integrationEvent.Currency,

            // The event does not carry an exponent. Two is the platform's default and the only value
            // any seeded currency uses; a wrong guess here would misplace a decimal point on
            // somebody's income, so it is stated rather than inferred.
            exponent: 2,
            $"donation.completed:{integrationEvent.DonationId:N}",
            integrationEvent.CompletedAt,
            cancellationToken,
            subjectType: "donation",
            subjectId: integrationEvent.DonationId);
    }
}

/// <summary>
/// A purchase made against something a creator published.
/// <para>
/// <b>The subject names the content, not its owner</b>, so the owner is resolved from this module's
/// own activity ledger — the entry written when the guide was published or the listing was created.
/// That is the same seam <see cref="IGuidesAuthorLookup"/> uses, and it is preferred to a project
/// reference into five vertical modules for a single Guid.
/// </para>
/// <para>
/// <b>Five subject types, and the rest earn nothing.</b> `MonetizationSubjectType` also carries
/// Member, Provider, Organization, Community and Campaign. A promoted organization is not a
/// creator's content, and a campaign is somebody buying advertising rather than earning from it —
/// attributing either would pay a creator for a purchase they had no part in.
/// </para>
/// </summary>
public sealed class MonetizationPurchaseEarningConsumer
    : IntegrationEventHandler<MonetizationProductPurchasedIntegrationEvent>
{
    private readonly CreatorEarningRecorder _recorder;
    private readonly Persistence.CreatorActivityLookup _lookup;

    public MonetizationPurchaseEarningConsumer(
        CreatorEarningRecorder recorder, Persistence.CreatorActivityLookup lookup)
    {
        _recorder = recorder;
        _lookup = lookup;
    }

    /// <summary>
    /// The activity ledger's own word for a monetization subject.
    /// <para>
    /// Returning null is a decision rather than a gap: it means "no creator earns from this", and
    /// every type omitted below is omitted for a stated reason.
    /// </para>
    /// </summary>
    private static string? LedgerSubjectFor(string? subjectType) => subjectType switch
    {
        "MarketplaceListing" => "listing",
        "HousingListing" => "housing",
        "Job" => "job",
        "Event" => "event",
        "Guide" => "guide",

        // `Update` is deliberately absent. The ledger records updates as `post`, and whether a
        // promoted update pays its author is a revenue decision nobody has made — guessing it here
        // would be the same fault as guessing a rate.
        _ => null,
    };

    protected override async Task HandleAsync(
        MonetizationProductPurchasedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (LedgerSubjectFor(integrationEvent.SubjectType) is not { } ledgerSubject)
        {
            return;
        }

        var ownerId = await _lookup.UserForSubjectAsync(
            ledgerSubject, integrationEvent.SubjectId, cancellationToken);

        if (ownerId is not { } owner)
        {
            return;
        }

        // **The buyer is not paid for buying.** Somebody promoting their own listing is the ordinary
        // case, and an earning there would hand back a share of what they just spent.
        if (owner == integrationEvent.BuyerUserId)
        {
            return;
        }

        await _recorder.RecordAsync(
            owner,
            CreatorEarningSource.SponsoredContent,
            integrationEvent.PaidMinorUnits,
            integrationEvent.Currency,
            exponent: 2,
            $"purchase.completed:{integrationEvent.PurchaseId:N}",
            integrationEvent.PurchasedAt,
            cancellationToken,
            ledgerSubject,
            integrationEvent.SubjectId);
    }
}
