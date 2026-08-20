using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Wallets;

/// <summary>
/// Applies a status change to a wallet.
/// <para>
/// A refusal from the aggregate — closing a wallet that still holds reserved funds — is translated
/// into a conflict rather than allowed to escape as an exception, with the aggregate's own message.
/// </para>
/// </summary>
public sealed class UpdateWalletStatusCommandHandler
    : ICommandHandler<UpdateWalletStatusCommand, WalletResponse>
{
    private readonly IWalletRepository _wallets;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateWalletStatusCommandHandler(
        IWalletRepository wallets,
        CurrencyCatalog currencies,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _wallets = wallets;
        _currencies = currencies;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<WalletResponse>> Handle(
        UpdateWalletStatusCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var wallet = await _wallets.FindAsync(WalletId.From(command.WalletId), cancellationToken);

        if (wallet is null)
        {
            return Result.Failure<WalletResponse>(Error.NotFound(
                FinancialErrorCodes.WalletNotFound, "That wallet does not exist."));
        }

        if (!Enum.TryParse<WalletStatus>(command.Status, true, out var status)
            || status == WalletStatus.Pending)
        {
            return Result.Failure<WalletResponse>(Error.Validation(
                FinancialErrorCodes.AmountInvalid,
                "A wallet may be set to Active, Frozen or Closed."));
        }

        try
        {
            switch (status)
            {
                case WalletStatus.Active:
                    wallet.Activate(_clock.UtcNow);
                    break;
                case WalletStatus.Frozen:
                    wallet.Freeze(command.Reason ?? "Frozen by an operator.", _clock.UtcNow);
                    break;
                default:
                    wallet.Close(_clock.UtcNow);
                    break;
            }
        }
        catch (DomainException exception)
        {
            return Result.Failure<WalletResponse>(
                Error.Conflict(exception.Code, exception.Message));
        }

        _wallets.Update(wallet);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var symbol = await _currencies.SymbolAsync(wallet.Currency, cancellationToken);

        return Result.Success(FinancialMapper.ToResponse(wallet, symbol));
    }
}
