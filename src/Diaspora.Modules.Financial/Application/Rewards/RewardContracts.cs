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
/// Awards points to a member. Staff, or a grantor's integration event.
/// <para>
/// <b>The grant reference is what stops a double award, and it belongs to the grantor.</b> Only
/// whoever recognised the earning can know whether two events describe one thing; B15 enforces
/// uniqueness per account and refuses the second silently, which is what an at-least-once bus
/// requires of everything downstream of it.
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
}
