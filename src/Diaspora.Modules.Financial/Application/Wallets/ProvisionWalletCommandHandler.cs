using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Wallets;

/// <summary>Opens an identity and its wallets, or returns what already exists.</summary>
public sealed class ProvisionWalletCommandHandler
    : ICommandHandler<ProvisionWalletCommand, IReadOnlyList<WalletResponse>>
{
    private readonly FinancialIdentityService _identities;
    private readonly CurrencyCatalog _currencies;
    private readonly IUnitOfWork _unitOfWork;

    public ProvisionWalletCommandHandler(
        FinancialIdentityService identities, CurrencyCatalog currencies, IUnitOfWork unitOfWork)
    {
        _identities = identities;
        _currencies = currencies;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<WalletResponse>>> Handle(
        ProvisionWalletCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!Enum.TryParse<FinancialAccountType>(command.AccountType, true, out var accountType)
            || accountType == FinancialAccountType.Platform)
        {
            return Result.Failure<IReadOnlyList<WalletResponse>>(Error.Validation(
                FinancialErrorCodes.AccountNotEligible,
                "That is not an account type this endpoint can open. The platform's own account is "
                + "opened by the seeder and by nothing else."));
        }

        var currency = command.Currency ?? _identities.DefaultCurrency;

        if (!await _currencies.IsSupportedAsync(currency, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<WalletResponse>>(Error.Validation(
                FinancialErrorCodes.CurrencyNotSupported,
                $"'{currency}' is not a currency this deployment denominates money in."));
        }

        var account = await _identities.GetOrCreateAsync(
            command.OwnerId,
            accountType,
            currency,
            command.DisplayName,
            command.CountryCode,
            cancellationToken);

        var wallets = await _identities.EnsureWalletsAsync(account, currency, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var symbol = await _currencies.SymbolAsync(currency, cancellationToken);

        return Result.Success<IReadOnlyList<WalletResponse>>(
            [.. wallets.Select(wallet => FinancialMapper.ToResponse(wallet, symbol))]);
    }
}
