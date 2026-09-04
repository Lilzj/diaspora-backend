using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Domain.Advertising;

/// <summary>
/// What an advertising campaign refuses, and why.
/// <para>
/// <b>Every refusal here names the state it refused from, not just that it refused.</b> A campaign
/// has twelve states and the advertiser is looking at one screen; "you cannot do that" tells them
/// nothing about which of the twelve they are in, and this surface has a paying customer on the
/// other end of it.
/// </para>
/// <para>
/// <b><see cref="AlreadyFinished"/> and the four terminal states.</b> Completed, Rejected, Cancelled
/// and Failed all refuse the same way, because from the caller's point of view they are one fact —
/// it is over — and the campaign itself already carries which one it was.
/// </para>
/// </summary>
public static class AdCampaignErrors
{
    // ── Drafting ────────────────────────────────────────────────────────────────────────────

    public static readonly Error OwnerRequired = Error.Validation(
        FinancialErrorCodes.AdCampaignOwnerRequired,
        "A campaign needs an advertiser.");

    public static readonly Error NameLength = Error.Validation(
        FinancialErrorCodes.AdCampaignNameLength,
        "A campaign name is between 3 and 120 characters.");

    public static readonly Error ObjectiveUnknown = Error.Validation(
        FinancialErrorCodes.AdCampaignObjectiveUnknown,
        "That is not an objective this platform offers.");

    /// <summary>
    /// A campaign with nothing to spend cannot deliver, and would sit active for ever showing zero.
    /// </summary>
    public static readonly Error BudgetRequired = Error.Validation(
        FinancialErrorCodes.AdCampaignBudgetRequired,
        "A campaign needs a budget above zero.");

    /// <summary>
    /// Unlike a fundraising campaign, an advertising one may not run open-ended: there would be no
    /// ceiling in time, and nothing for the completion sweep to fire on.
    /// </summary>
    public static readonly Error ScheduleRequiresEnd = Error.Validation(
        FinancialErrorCodes.AdCampaignScheduleRequiresEnd,
        "Say when the campaign should stop.");

    public static readonly Error ScheduleAlreadyOver = Error.Validation(
        FinancialErrorCodes.AdCampaignScheduleAlreadyOver,
        "That campaign's dates have already passed. Change them before submitting it.");

    // ── Editing ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Once funded, the budget is a sum somebody paid and the audience is what a reviewer approved.
    /// Letting either move would mean the thing that ran was not the thing that was bought.
    /// </summary>
    public static readonly Error NotEditable = Error.Conflict(
        FinancialErrorCodes.AdCampaignNotEditable,
        "This campaign has been submitted and can no longer be changed.");

    // ── Submission ──────────────────────────────────────────────────────────────────────────

    public static readonly Error NotSubmittable = Error.Conflict(
        FinancialErrorCodes.AdCampaignNotSubmittable,
        "Only a draft campaign can be submitted.");

    public static readonly Error NotAwaitingPayment = Error.Conflict(
        FinancialErrorCodes.AdCampaignNotAwaitingPayment,
        "This campaign is not waiting for payment.");

    // ── Review ──────────────────────────────────────────────────────────────────────────────

    public static readonly Error NotReviewable = Error.Conflict(
        FinancialErrorCodes.AdCampaignNotReviewable,
        "This campaign is not waiting to be reviewed.");

    /// <summary>A rejection without a reason is one the advertiser can neither act on nor appeal.</summary>
    public static readonly Error RejectionNeedsReason = Error.Validation(
        FinancialErrorCodes.AdCampaignRejectionNeedsReason,
        "Say why the campaign was rejected. The advertiser is shown this.");

    public static readonly Error NothingToAppeal = Error.Conflict(
        FinancialErrorCodes.AdCampaignNothingToAppeal,
        "Only a rejected campaign can be appealed.");

    public static readonly Error AlreadyAppealed = Error.Conflict(
        FinancialErrorCodes.AdCampaignAlreadyAppealed,
        "This campaign has already been appealed once.");

    public static readonly Error AppealNeedsReason = Error.Validation(
        FinancialErrorCodes.AdCampaignAppealNeedsReason,
        "Say why you think the decision was wrong.");

    // ── Delivery ────────────────────────────────────────────────────────────────────────────

    public static readonly Error NotSchedulable = Error.Conflict(
        FinancialErrorCodes.AdCampaignNotSchedulable,
        "Only an approved campaign can be scheduled.");

    public static readonly Error NotActivatable = Error.Conflict(
        FinancialErrorCodes.AdCampaignNotActivatable,
        "This campaign is not ready to start.");

    public static readonly Error ScheduleNotStarted = Error.Conflict(
        FinancialErrorCodes.AdCampaignScheduleNotStarted,
        "This campaign has not reached its start date.");

    public static readonly Error NotPausable = Error.Conflict(
        FinancialErrorCodes.AdCampaignNotPausable,
        "Only a running campaign can be paused.");

    public static readonly Error NotResumable = Error.Conflict(
        FinancialErrorCodes.AdCampaignNotResumable,
        "Only a paused campaign can be resumed.");

    /// <summary>
    /// The one that matters most. A paused campaign that kept charging would leave the advertiser
    /// with a number going up and no way to see why.
    /// </summary>
    public static readonly Error NotDelivering = Error.Conflict(
        FinancialErrorCodes.AdCampaignNotDelivering,
        "This campaign is not running, so nothing can be charged against it.");

    public static readonly Error CurrencyMismatch = Error.Validation(
        FinancialErrorCodes.AdCampaignCurrencyMismatch,
        "That amount is in a different currency from the campaign's budget.");

    public static readonly Error SpendMustBePositive = Error.Validation(
        FinancialErrorCodes.AdCampaignSpendMustBePositive,
        "Recorded spend has to be more than nothing.");

    // ── Endings ─────────────────────────────────────────────────────────────────────────────

    public static readonly Error AlreadyFinished = Error.Conflict(
        FinancialErrorCodes.AdCampaignAlreadyFinished,
        "This campaign has already finished.");

    // ── Placements ──────────────────────────────────────────────────────────────────────────

    public static readonly Error PlacementAlreadyBooked = Error.Conflict(
        FinancialErrorCodes.AdCampaignPlacementAlreadyBooked,
        "That placement is already part of this campaign.");

    // ── Existence ───────────────────────────────────────────────────────────────────────────

    public static readonly Error NotFound = Error.NotFound(
        FinancialErrorCodes.AdCampaignNotFound,
        "There is no such campaign.");
}
