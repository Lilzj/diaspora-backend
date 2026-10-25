using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using FluentValidation;

namespace Diaspora.Modules.Financial.Application.Rewards;

/// <summary>The caller's points balance.</summary>
[Authorize(Permission = FinancialPermissions.WalletRead)]
public sealed record GetMyRewardBalanceQuery : IQuery<RewardBalanceResponse>;

/// <summary>How the caller earned and spent their points.</summary>
[Authorize(Permission = FinancialPermissions.WalletRead)]
public sealed record GetMyRewardHistoryQuery : IQuery<IReadOnlyList<RewardEntryResponse>>
{
    /// <summary>The instant the previous page ended on. Inclusive — see <see cref="ExcludeIds"/>.</summary>
    public DateTimeOffset? Before { get; init; }

    /// <summary>
    /// The entries already shown at <see cref="Before"/>.
    /// <para>
    /// <b>Inclusive boundary, minus what was shown.</b> This read used to take the boundary alone
    /// and compare it non-strictly, so every page began with the row the previous page ended on —
    /// and a page whose entries all shared one instant returned itself for ever, which a client
    /// paging on "a full page means there is more" scrolls without end. A strict comparison would
    /// have traded that for the opposite defect: entries sharing the boundary instant, which a
    /// batch of awards written in one unit of work produces routinely, would never be shown at all.
    /// </para>
    /// <para>
    /// This is the shape every other paged read in this module uses, including
    /// <c>/rewards/conversions</c> in the same file. This route was the one that never got it.
    /// </para>
    /// </summary>
    public IReadOnlyList<Guid> ExcludeIds { get; init; } = [];

    public int Take { get; init; } = Domain.Policies.FinancialLimits.DefaultLedgerPageSize;
}

/// <summary>
/// What points are worth today, and how many may be converted.
/// <para>
/// Read before the conversion screen so the client never invents a rate. §12 of the frontend brief
/// forbids computing one locally, and this is the contract that makes obeying it possible.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.WalletRead)]
public sealed record GetRewardConversionTermsQuery : IQuery<RewardConversionTermsResponse>
{
    /// <summary>Optional. A currency other than the rate's reads as disabled, with the reason.</summary>
    public string? Currency { get; init; }

    /// <summary>Ignored. The rate's country is the member's account's, never the request's.</summary>
    public string? CountryCode { get; init; }
}

/// <summary>
/// What converting this many points would produce.
/// <para>
/// A request rather than a calculation, even though the rate is on the terms — the server is the only
/// thing that may decide what a member gets, and a preview computed on the device disagrees with it
/// at the first rounding boundary.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.WalletRead)]
public sealed record PreviewRewardConversionQuery : IQuery<RewardConversionPreviewResponse>
{
    public required long Points { get; init; }

    /// <summary>Optional. When given, it must be the currency the rate is in.</summary>
    public string? Currency { get; init; }

    /// <summary>Ignored. The rate's country is the member's account's, never the request's.</summary>
    public string? CountryCode { get; init; }
}

/// <summary>
/// Turns points into credit.
/// <para>
/// <b>Transactional, because points leaving and money arriving are one change.</b> A conversion that
/// debited the points and failed to post the credit would be a member paying for nothing; one that
/// posted the credit and failed to debit the points would be a member converting the same points for
/// ever.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PaymentsManage)]
[Transactional(Reason = "Points leaving and credit arriving are one change or neither.")]
[Auditable(Action = "financial.rewards.convert")]
public sealed record ConvertRewardPointsCommand : ICommand<RewardConversionResponse>
{
    public required long Points { get; init; }

    /// <summary>Optional. When given, it must be the currency the rate is in.</summary>
    public string? Currency { get; init; }

    /// <summary>Ignored. The rate's country is the member's account's, never the request's.</summary>
    public string? CountryCode { get; init; }

    /// <summary>
    /// Required: from the <c>Idempotency-Key</c> header, or the body. Unique per member — two members
    /// choosing the same key convert independently.
    /// </summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// A conversion must carry its key.
/// <para>
/// It was optional, and a keyless conversion had no replay at all: a double-tapped button or a
/// retried request converted twice, and only the balance stopped the second. Every client of this
/// route already sends one. Points are left to the pricer, whose refusal says why in the member's
/// terms rather than as a validation error.
/// </para>
/// </summary>
public sealed class ConvertRewardPointsCommandValidator : AbstractValidator<ConvertRewardPointsCommand>
{
    public ConvertRewardPointsCommandValidator()
    {
        RuleFor(command => command.IdempotencyKey)
            .NotEmpty()
            .WithMessage("A conversion needs an idempotency key, in the Idempotency-Key header or the body.")
            .MaximumLength(Domain.Policies.FinancialLimits.MaxReferenceLength);

        RuleFor(command => command.Currency)
            .Length(3)
            .When(command => !string.IsNullOrWhiteSpace(command.Currency))
            .WithMessage("A currency is three ISO 4217 letters.");
    }
}

/// <summary>The caller's conversions.</summary>
[Authorize(Permission = FinancialPermissions.WalletRead)]
public sealed record GetMyRewardConversionsQuery : IQuery<IReadOnlyList<RewardConversionResponse>>
{
    public DateTimeOffset? Before { get; init; }

    public IReadOnlyList<Guid> ExcludeIds { get; init; } = [];

    public int Take { get; init; } = Domain.Policies.FinancialLimits.DefaultLedgerPageSize;
}

/// <summary>
/// Awards points to a member by hand. Staff only: a grantor's integration event awards through
/// <c>RewardPointsGrantedConsumer</c>, which never sends this command.
/// <para>
/// <b>The grant reference is what stops a double award, and it belongs to the grantor.</b> Only
/// whoever recognised the earning can know whether two events describe one thing; B15 enforces
/// uniqueness per account and refuses the second silently, which is what an at-least-once bus
/// requires of everything downstream of it.
/// </para>
/// <para>
/// <b>Bounded by the <c>reward.grant</c> policy, and it says why.</b> This was the one route that
/// creates points with no earning behind them, and it had no upper bound: one staff account could
/// mint any balance. A grant now carries a reason, stored on its entry with the grantor, and is
/// refused above the policy's per-grant maximum or past its per-grantor daily ceiling. A second
/// approver above a threshold is still to come.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.CreditManage)]
[Transactional(Reason = "Opening the account and awarding against it are one change.")]
[Auditable(Action = "financial.rewards.grant")]
public sealed record GrantRewardPointsCommand : ICommand<RewardBalanceResponse>
{
    public required Guid UserId { get; init; }

    public required long Points { get; init; }

    public required string Source { get; init; }

    public required string GrantReference { get; init; }

    /// <summary>Why the member is being given these points. Stored on the entry, with the grantor.</summary>
    public required string Reason { get; init; }
}

/// <summary>
/// Holds a member's points from conversion, or lifts the hold. Staff.
/// <para>
/// <b>The caller <c>BlockConversion</c> never had.</b> The hold existed on the account and the
/// conversion path honoured it, but nothing could place one. The permission is the wallet freeze's:
/// a hold is the same act for points, and it is admin-only for the same reason.
/// </para>
/// <para>
/// <b>Every decision is kept on the account,</b> with who made it and why, because the audit entry
/// this attribute writes is a log line and the hold's own reason is cleared when it lifts.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.WalletManage)]
[Transactional(Reason = "Opening the account and holding it are one change.")]
[Auditable(Action = "financial.rewards.conversion_hold")]
public sealed record SetRewardConversionHoldCommand : ICommand<RewardConversionHoldResponse>
{
    public required Guid UserId { get; init; }

    /// <summary>True to hold, false to lift.</summary>
    public required bool IsHeld { get; init; }

    public required string Reason { get; init; }
}

/// <summary>A hold, and a lift, must say why.</summary>
public sealed class SetRewardConversionHoldCommandValidator
    : AbstractValidator<SetRewardConversionHoldCommand>
{
    public SetRewardConversionHoldCommandValidator()
    {
        RuleFor(command => command.Reason)
            .NotEmpty()
            .WithMessage("A hold needs a reason, and so does lifting one.")
            .MaximumLength(Domain.Policies.FinancialLimits.MaxReasonLength);
    }
}

/// <summary>A staff grant must say why.</summary>
public sealed class GrantRewardPointsCommandValidator : AbstractValidator<GrantRewardPointsCommand>
{
    public GrantRewardPointsCommandValidator()
    {
        RuleFor(command => command.Reason)
            .NotEmpty()
            .WithMessage("A grant needs a reason: why is this member being given points?")
            .MaximumLength(Domain.Policies.FinancialLimits.MaxReasonLength);

        RuleFor(command => command.GrantReference)
            .NotEmpty()
            .MaximumLength(Domain.Policies.FinancialLimits.MaxReferenceLength);
    }
}
