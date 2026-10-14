using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Modules.Financial.Extensibility.Provisioning;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// A member's currency comes from where they live, never from a hard-coded GBP.
/// <para>
/// <b>The defect these pin.</b> Six Financial handlers, the creator revenue summary and the
/// advertising dashboard answered "no currency was named" with the configured default, so a member
/// resident in Mauritius was quoted limits, terms and empty totals in sterling. They now ask
/// <see cref="MemberCurrency"/>, and these assert its order: money already held, then the named
/// country, then the member's residence, then the default.
/// </para>
/// </summary>
public sealed class MemberCurrencyTests
{
    private readonly IFinancialAccountRepository _accounts = Substitute.For<IFinancialAccountRepository>();
    private readonly IMemberProfileService _members = Substitute.For<IMemberProfileService>();

    [Fact]
    public async Task A_member_resident_in_Mauritius_with_no_account_gets_MUR()
    {
        var userId = Guid.NewGuid();
        Resident(userId, "MU");

        var currency = await Subject().ForMemberAsync(userId);

        currency.Should().Be("MUR");
    }

    [Fact]
    public async Task Money_already_held_keeps_its_currency_whatever_the_residence()
    {
        // A member opened in GBP before the fix, now resident in Mauritius. Restating their money
        // in MUR would be a conversion, and the platform never converts.
        var userId = Guid.NewGuid();
        Resident(userId, "MU");
        HasAccount(userId, FinancialAccountType.Personal, "GBP");

        var currency = await Subject().ForMemberAsync(userId);

        currency.Should().Be("GBP");
    }

    [Fact]
    public async Task A_residence_the_platform_has_no_currency_for_falls_back_to_the_configured_default()
    {
        var userId = Guid.NewGuid();
        Resident(userId, "ZZ");

        var currency = await Subject(defaultCurrency: "USD").ForMemberAsync(userId);

        currency.Should().Be("USD");
    }

    [Fact]
    public async Task An_unanswered_profile_lookup_is_not_a_country_and_falls_back_to_the_default()
    {
        _members.IsAvailable.Returns(false);

        var currency = await Subject(defaultCurrency: "USD").ForMemberAsync(Guid.NewGuid());

        currency.Should().Be("USD");
    }

    [Fact]
    public async Task An_organization_wallet_takes_the_named_country_and_never_asks_identity()
    {
        var currency = await Subject().ForOwnerAsync(
            Guid.NewGuid(), FinancialAccountType.Organization, "MU");

        currency.Should().Be("MUR");
        await _members.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default);
    }

    [Fact]
    public async Task A_personal_wallet_with_no_named_country_takes_the_owner_residence()
    {
        var userId = Guid.NewGuid();
        Resident(userId, "MU");

        var currency = await Subject().ForOwnerAsync(
            userId, FinancialAccountType.Personal, countryCode: null);

        currency.Should().Be("MUR");
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────

    private void Resident(Guid userId, string country)
    {
        _members.IsAvailable.Returns(true);
        _members
            .ResolveAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, MemberProfile>
            {
                [userId] = new()
                {
                    UserId = userId,
                    DisplayName = "Anjali Ramdin",
                    CountryOfResidence = country
                }
            });
    }

    private void HasAccount(Guid ownerId, FinancialAccountType type, string currency) =>
        _accounts
            .FindByOwnerAsync(ownerId, type, Arg.Any<CancellationToken>())
            .Returns(FinancialAccount.Open(
                FinancialAccountId.New(),
                ownerId,
                type,
                currency,
                "A member",
                DateTimeOffset.UtcNow));

    private MemberCurrency Subject(string defaultCurrency = "GBP")
    {
        var identities = new FinancialIdentityService(
            _accounts,
            Substitute.For<IWalletRepository>(),
            Substitute.For<IWalletProvisioningPolicy>(),
            Substitute.For<IGuidGenerator>(),
            Substitute.For<IClock>(),
            Microsoft.Extensions.Options.Options.Create(
                new FinancialOptions { DefaultCurrency = defaultCurrency }));

        return new MemberCurrency(_accounts, _members, identities);
    }
}
