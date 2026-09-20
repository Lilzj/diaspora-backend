using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// What a cap means on a rule that belongs to a country.
/// <para>
/// <b>Two defects that cancelled each other into silence.</b> <c>ApplyAwardAsync</c> carried a
/// comment saying "verifying the same country twice should not pay twice", and nothing enforced
/// it: <c>CountryVerified</c> declared no <c>MaxOccurrences</c>, so every producer that scored it
/// was paid. That is how two duplicate consumers came to pay thirty points twice for a single
/// approval without any cap noticing.
/// </para>
/// <para>
/// The obvious repair would have broken something else. Usage was counted per action with no
/// country at all, so <c>MaxOccurrences = 1</c> would have meant once in a member's <em>life</em>
/// — and a member who moves country and verifies where they now live would have been paid
/// nothing for it. Recording a move became an ordinary thing to do the same week.
/// </para>
/// <para>
/// So a cap on a country-scoped rule counts within its country, and the cooldown deliberately
/// does not: a cap is about what a thing is worth, a cooldown about how fast it can be farmed,
/// and a per-country cooldown would let one burst through once for every country on the platform.
/// </para>
/// </summary>
public sealed class CountryScopedCapTests : IdentityTestBase
{
    public CountryScopedCapTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// The defect. Scoring the same country twice must pay once.
    /// </summary>
    [Fact]
    public async Task The_same_country_verified_twice_pays_once()
    {
        var member = await VerifiedInAsync("MU");

        await ScoreCountryAgainAsync(member, "MU");

        (await PointsForAsync(member, "MU")).Should().Be(
            30, "a second producer for one approval must not be a second payment");
    }

    /// <summary>
    /// The thing the naive fix would have broken. A member who moves is verified somewhere new,
    /// and that is a different country's standing to build.
    /// </summary>
    [Fact]
    public async Task A_second_country_is_still_paid()
    {
        var member = await VerifiedInAsync("MU");

        await ScoreCountryAgainAsync(member, "GB");

        (await PointsForAsync(member, "MU")).Should().Be(30);
        (await PointsForAsync(member, "GB")).Should().Be(
            30, "moving country is not the same country over again");
    }

    /// <summary>
    /// The standing follows the points, because a country-scoped rule moves both. A member who
    /// was paid twice would also have carried double standing in that community.
    /// </summary>
    [Fact]
    public async Task The_country_standing_is_not_doubled_either()
    {
        var member = await VerifiedInAsync("MU");

        var before = await StandingPointsAsync(member, "MU");

        await ScoreCountryAgainAsync(member, "MU");

        (await StandingPointsAsync(member, "MU")).Should().Be(before);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A member verified in one country through the real submission and review, leaving an
    /// Admin authenticated.
    /// </summary>
    private async Task<Guid> VerifiedInAsync(string countryCode)
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        Api.Authenticate(auth.Tokens.AccessToken);

        var submitted = await HttpClient.PostAsJsonAsync(
            "/api/v1/trust/verifications/country",
            new
            {
                countryCode,
                documentType = "UtilityBill",
                documentReference = "media:" + Guid.NewGuid(),
                arrivalDate = new DateOnly(2019, 3, 1)
            });

        submitted.IsSuccessStatusCode.Should().BeTrue(
            await submitted.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await submitted.Content.ReadAsStringAsync());
        var verificationId = document.RootElement.GetProperty("data")
            .GetProperty("verificationId").GetGuid();

        await SignInAsRoleAsync("Admin");

        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/trust/verifications/{verificationId}/assign", new { }))
            .EnsureSuccessStatusCode();

        (await HttpClient.PostAsJsonAsync(
                $"/api/v1/trust/verifications/{verificationId}/review",
                new { decision = "Approve", notes = "Checks out." }))
            .EnsureSuccessStatusCode();

        await Factory.DrainOutboxAsync();

        return auth.User.Id;
    }

    /// <summary>
    /// A second producer scoring the same action, which is exactly what the removed duplicate
    /// consumers did. Goes through the service rather than a route, because the defect was never
    /// reachable from one.
    /// </summary>
    private async Task ScoreCountryAgainAsync(Guid userId, string countryCode)
    {
        using var scope = Factory.Services.CreateScope();

        await scope.ServiceProvider
            .GetRequiredService<ReputationService>()
            .ScoreAsync(
                UserId.From(userId),
                ReputationActions.CountryVerified,
                "verification",
                Guid.NewGuid().ToString(),
                countryCode);

        await scope.ServiceProvider
            .GetRequiredService<IUnitOfWork>()
            .SaveChangesAsync();
    }

    private async Task<int> PointsForAsync(Guid userId, string countryCode)
    {
        using var scope = Factory.Services.CreateScope();

        var page = await scope.ServiceProvider
            .GetRequiredService<IReputationProfileRepository>()
            .ListEventsAsync(UserId.From(userId), new PaginationRequest { PageSize = 50 });

        return page.Items
            .Where(entry => entry.ActionType == ReputationActions.CountryVerified
                && string.Equals(entry.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase))
            .Sum(entry => entry.Points);
    }

    private async Task<int> StandingPointsAsync(Guid userId, string countryCode)
    {
        using var scope = Factory.Services.CreateScope();

        var profile = await scope.ServiceProvider
            .GetRequiredService<ITrustProfileRepository>()
            .GetByUserAsync(UserId.From(userId));

        return profile?.Countries
            .FirstOrDefault(standing => string.Equals(
                standing.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase))
            ?.Points ?? 0;
    }
}
