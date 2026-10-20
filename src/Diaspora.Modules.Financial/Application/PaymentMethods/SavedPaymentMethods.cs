using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.PaymentMethods;

/// <summary>
/// The reusable payment instruments the platform holds for the caller.
/// <para>
/// <b>What is stored, exactly.</b> Per payment, <c>financial_payment_intents</c> keeps the method
/// <em>kind</em> (<c>Card</c>, <c>BankTransfer</c>, <c>MobileMoney</c>, <c>Wallet</c>,
/// <c>ManualTransfer</c>, <c>Credit</c>), the adapter's name, the provider's payment reference and
/// its status string — enough to reconcile one payment, and nothing that can charge again. No card
/// number, brand, last four digits, expiry, provider customer id, payment-method id or vault token is
/// written anywhere.
/// </para>
/// <para>
/// <b>Why there is nothing to list.</b> The only card adapter creates a hosted checkout session
/// per payment with no provider customer and no instruction to keep the card for later use, so the
/// provider holds no reusable method on the platform's behalf either. The other adapters pay out;
/// they take no money in. A member re-enters their details on the provider's page every time.
/// </para>
/// <para>
/// Payout destinations — where a withdrawal is sent — are stored, but they are money going out, not
/// a way to pay, and are served by the withdrawal routes rather than here.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PaymentsRead)]
public sealed record GetMySavedPaymentMethodsQuery : IQuery<IReadOnlyList<SavedPaymentMethodResponse>>;

/// <summary>
/// Forgets a saved payment method. Answers not-found for every identifier today, because none is
/// stored; see <see cref="GetMySavedPaymentMethodsQuery"/>.
/// </summary>
[Authorize(Permission = FinancialPermissions.PaymentsManage)]
public sealed record ForgetSavedPaymentMethodCommand : ICommand<SavedPaymentMethodRemovalResponse>
{
    public required Guid PaymentMethodId { get; init; }
}

/// <summary>
/// Answers both routes from what the platform actually keeps, which is no reusable method.
/// <para>
/// Not a stub waiting for data: there is no table to read because nothing is vaulted, and inventing
/// one — or vaulting with a provider to have something to show — is a product and compliance
/// decision this route does not make. When an adapter starts keeping instruments, the list reads its
/// table and the forget detaches it at the provider before deleting the row.
/// </para>
/// </summary>
public sealed class SavedPaymentMethodHandlers
    : IQueryHandler<GetMySavedPaymentMethodsQuery, IReadOnlyList<SavedPaymentMethodResponse>>,
        ICommandHandler<ForgetSavedPaymentMethodCommand, SavedPaymentMethodRemovalResponse>
{
    private readonly FinancialAuthorizationService _authorization;

    public SavedPaymentMethodHandlers(FinancialAuthorizationService authorization) =>
        _authorization = authorization;

    public Task<Result<IReadOnlyList<SavedPaymentMethodResponse>>> Handle(
        GetMySavedPaymentMethodsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        return Task.FromResult(caller.IsFailure
            ? Result.Failure<IReadOnlyList<SavedPaymentMethodResponse>>(caller.Error)
            : Result.Success<IReadOnlyList<SavedPaymentMethodResponse>>([]));
    }

    public Task<Result<SavedPaymentMethodRemovalResponse>> Handle(
        ForgetSavedPaymentMethodCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Task.FromResult(Result.Failure<SavedPaymentMethodRemovalResponse>(caller.Error));
        }

        return Task.FromResult(Result.Failure<SavedPaymentMethodRemovalResponse>(Error.NotFound(
            FinancialErrorCodes.PaymentMethodNotFound,
            "There is no saved payment method with that identifier. Diaspora does not keep cards "
            + "or accounts between payments.")));
    }
}
