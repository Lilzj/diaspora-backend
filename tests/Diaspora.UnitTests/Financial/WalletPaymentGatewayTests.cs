using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Modules.Financial.Extensibility.Gateways;
using Diaspora.Modules.Financial.Extensibility.Provisioning;
using Diaspora.Modules.Financial.Infrastructure.Gateways;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.UnitTests.Financial.TestData;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// Paying with money the platform already holds for you.
/// <para>
/// <b>The refusals matter more than the success here.</b> A wallet payment that goes through when it
/// should not is a member spending money they do not have, and the ledger's overdraw rule would
/// catch it — but only as an unexplained posting failure at the end of a checkout. These are the
/// answers that reach the member instead.
/// </para>
/// </summary>
public sealed class WalletPaymentGatewayTests
{
    private static readonly Guid Account = Guid.NewGuid();

    private readonly IWalletRepository _wallets = Substitute.For<IWalletRepository>();

    private WalletPaymentGateway Gateway()
    {
        var identities = new FinancialIdentityService(
            Substitute.For<IFinancialAccountRepository>(),
            _wallets,
            Substitute.For<IWalletProvisioningPolicy>(),
            Substitute.For<IGuidGenerator>(),
            Substitute.For<IClock>(),
            Microsoft.Extensions.Options.Options.Create(new FinancialOptions()));

        return new WalletPaymentGateway(identities);
    }

    private static PaymentGatewayRequest Request(long minorUnits = 5_000, Guid? account = null) =>
        new()
        {
            PaymentIntentId = Guid.NewGuid(),
            Amount = Money.FromMinorUnits(minorUnits, "GBP", 2),
            MethodKind = PaymentMethodKind.Wallet,
            Description = "A thing",
            AccountId = account ?? Account,
        };

    private Wallet GiveTheMember(long availableMinorUnits, bool active = true)
    {
        var wallet = FinancialTestData.Wallet_(active: active);

        if (availableMinorUnits > 0)
        {
            // Credited through the aggregate rather than by setting a field, so the balance under
            // test is one the domain would actually produce.
            wallet.Apply(LedgerEntry.Append(
                LedgerEntryId.New(),
                wallet.Id,
                TransactionId.New(),
                wallet.NextSequence,
                LedgerEntryType.Credit,
                LedgerDirection.Credit,
                LedgerBucket.Available,
                Money.FromMinorUnits(availableMinorUnits, "GBP", 2),
                0,
                FinancialTestData.Stamp(),
                FinancialTestData.Now));
        }

        _wallets
            .FindByAccountAsync(
                Arg.Any<FinancialAccountId>(),
                WalletType.Primary,
                "GBP",
                Arg.Any<CancellationToken>())
            .Returns(wallet);

        return wallet;
    }

    [Fact]
    public async Task It_takes_a_payment_the_balance_covers()
    {
        GiveTheMember(10_000);

        var result = await Gateway().CaptureAsync(Request(5_000));

        result.Outcome.Should().Be(GatewayOutcome.Succeeded);
    }

    [Fact]
    public async Task It_takes_a_payment_the_balance_covers_exactly()
    {
        // The boundary, because an off-by-one here refuses somebody spending their whole balance —
        // which is the most ordinary thing a member does with a wallet.
        GiveTheMember(5_000);

        var result = await Gateway().CaptureAsync(Request(5_000));

        result.Outcome.Should().Be(GatewayOutcome.Succeeded);
    }

    [Fact]
    public async Task It_refuses_a_payment_the_balance_does_not_cover()
    {
        GiveTheMember(4_999);

        var result = await Gateway().CaptureAsync(Request(5_000));

        result.Outcome.Should().Be(GatewayOutcome.Declined);
        result.FailureCode.Should().Be("wallet_insufficient_balance");

        // The member is told what to do about it, not just that it failed.
        result.FailureMessage.Should().Contain("Add money");
    }

    [Fact]
    public async Task It_refuses_when_the_member_has_no_wallet_in_that_currency()
    {
        _wallets
            .FindByAccountAsync(
                Arg.Any<FinancialAccountId>(),
                WalletType.Primary,
                "GBP",
                Arg.Any<CancellationToken>())
            .Returns((Wallet?)null);

        var result = await Gateway().CaptureAsync(Request());

        result.Outcome.Should().Be(GatewayOutcome.Declined);
        result.FailureCode.Should().Be("wallet_missing");

        // Named, because "you have no balance" and "you have no GBP balance" are different problems
        // for somebody who holds money in another currency.
        result.FailureMessage.Should().Contain("GBP");
    }

    [Fact]
    public async Task It_refuses_a_payment_with_no_account_behind_it()
    {
        var result = await Gateway().CaptureAsync(
            new PaymentGatewayRequest
            {
                PaymentIntentId = Guid.NewGuid(),
                Amount = Money.FromMinorUnits(5_000, "GBP", 2),
                MethodKind = PaymentMethodKind.Wallet,
                Description = "A thing",
                AccountId = null,
            });

        result.Outcome.Should().Be(GatewayOutcome.Declined);
        result.FailureCode.Should().Be("wallet_account_unknown");
    }

    [Fact]
    public async Task Authorising_is_the_same_answer_as_capturing()
    {
        // `SupportsSeparateAuthorization` is false, so the processor calls capture where it would
        // otherwise authorise. Both paths existing and disagreeing is how a payment gets taken twice.
        GiveTheMember(10_000);

        var gateway = Gateway();

        var authorised = await gateway.AuthorizeAsync(Request(5_000));
        var captured = await gateway.CaptureAsync(Request(5_000));

        gateway.SupportsSeparateAuthorization.Should().BeFalse();
        authorised.Outcome.Should().Be(captured.Outcome);
    }

    [Fact]
    public void It_handles_only_the_wallet_method()
    {
        // The registry resolves by method, and claiming Card here would put every card payment on an
        // adapter that cannot reach a card network.
        Gateway().SupportedMethods.Should().BeEquivalentTo(new[] { PaymentMethodKind.Wallet });
    }

    [Fact]
    public void It_restricts_no_currency()
    {
        // A member holds a wallet per currency, so the currency question is answered by whether they
        // have that wallet with enough in it. A list kept here would go stale the day a currency is
        // added and refuse payments the platform can make.
        Gateway().SupportedCurrencies.Should().BeEmpty();
    }
}
