using System.Net.Http.Headers;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Extensibility.Currency;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// A member's currency comes from their country of residence, never from a hard-coded GBP.
/// <para>
/// <b>The defect these pin (14 Oct 2026).</b> Account opening already took the currency from the
/// member's residence, but every surface that was not handed a currency — deposit and withdrawal
/// limits, reward conversion terms, wallet provisioning, the creator revenue summary with no
/// earnings yet, the advertising dashboard with nothing served — fell straight back to the
/// configured default, so a member resident in Mauritius was quoted in sterling. Each of these
/// registers a member resident in MU and reads one of those surfaces without naming a currency.
/// </para>
/// </summary>
[Collection(FinanceCollection.Name)]
public sealed class ResidenceCurrencyTests : PooledIdentityTestBase
{
    public ResidenceCurrencyTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Deposit_limits_for_a_Mauritian_resident_are_quoted_in_MUR()
    {
        await AuthenticateResidentAsync("MU");

        var limits = await IdentityApiClient.ReadDataAsync<LimitsShape>(
            await HttpClient.GetAsync(new Uri(
                "/api/v1/financial/wallets/me/deposits/limits", UriKind.Relative)));

        limits.Currency.Should().Be("MUR", "the member lives in Mauritius and named no currency");
    }

    [Fact]
    public async Task Withdrawal_limits_for_a_Mauritian_resident_are_quoted_in_MUR()
    {
        await AuthenticateResidentAsync("MU");

        var limits = await IdentityApiClient.ReadDataAsync<LimitsShape>(
            await HttpClient.GetAsync(new Uri(
                "/api/v1/financial/wallets/me/withdrawals/limits", UriKind.Relative)));

        limits.Currency.Should().Be("MUR");
    }

    [Fact]
    public async Task A_creator_with_no_earnings_reads_an_empty_summary_in_their_own_currency()
    {
        await AuthenticateResidentAsync("MU");

        var summary = await IdentityApiClient.ReadDataAsync<SummaryShape>(
            await HttpClient.GetAsync(new Uri(
                "/api/v1/creators/me/revenue/summary", UriKind.Relative)));

        summary.Currency.Should().Be("MUR", "this was a literal GBP when there were no earning rows");
        summary.LifetimeMinorUnits.Should().Be(0);
    }

    [Fact]
    public async Task Provisioning_a_wallet_with_no_currency_named_opens_it_in_the_residence_currency()
    {
        var userId = await AuthenticateResidentAsync("MU");

        using var scope = Factory.Services.CreateScope();

        var resolver = scope.ServiceProvider.GetRequiredService<MemberCurrency>();

        // The same call ProvisionWalletCommandHandler makes when the request names no currency.
        (await resolver.ForOwnerAsync(userId, FinancialAccountType.Personal, countryCode: null))
            .Should().Be("MUR");

        (await scope.ServiceProvider.GetRequiredService<IMemberCurrency>().ForMemberAsync(userId))
            .Should().Be("MUR");
    }

    [Fact]
    public async Task A_British_resident_still_gets_GBP()
    {
        var userId = await AuthenticateResidentAsync("GB");

        using var scope = Factory.Services.CreateScope();

        (await scope.ServiceProvider.GetRequiredService<IMemberCurrency>().ForMemberAsync(userId))
            .Should().Be("GBP");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<Guid> AuthenticateResidentAsync(string country)
    {
        var (_, auth) = await RegisterVerifiedMemberAsync(countryOfResidence: country);

        Api.Authenticate(auth.Tokens.AccessToken);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);

        return auth.User.Id;
    }

    private sealed record LimitsShape
    {
        public string Currency { get; init; } = string.Empty;
    }

    private sealed record SummaryShape
    {
        public string Currency { get; init; } = string.Empty;

        public long LifetimeMinorUnits { get; init; }
    }
}
