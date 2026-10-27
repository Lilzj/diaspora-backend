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
/// A published guide earns XP up to three a day, every guide (REP-09).
/// <para>
/// The rule used to carry a one-hour cooldown, which is a gap between awards: the second
/// <em>distinct</em> guide a member published inside the hour earned nothing, and so did every one
/// after it until the hour passed. Each guide is already paid once by its award key, so what is left
/// to bound is how fast the XP can climb, and a cap over a rolling day says that directly.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class GuidePublishedDailyCapTests : PooledIdentityTestBase
{
    private const string Guides = "/api/v1/guides";

    public GuidePublishedDailyCapTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Two_guides_published_minutes_apart_both_pay()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await PublishGuideAsync("Opening a bank account");
        await PublishGuideAsync("Registering with a doctor");
        await Factory.DrainOutboxAsync();

        (await GuideAwardsAsync(auth.User.Id)).Should().HaveCount(2, "each is a different guide");
    }

    [Fact]
    public async Task The_fourth_guide_in_a_day_pays_nothing()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        foreach (var title in new[] { "Renting a flat", "Buying a SIM card", "Paying council tax", "Finding a school" })
        {
            await PublishGuideAsync(title);
        }

        await Factory.DrainOutboxAsync();

        (await GuideAwardsAsync(auth.User.Id)).Should().HaveCount(3);

        var profile = await ProfileAsync(auth.User.Id);
        profile.GuidesPublished.Should().Be(3);
        profile.ContributionScore.Should().Be(150, "the XP for a guide is unchanged (D-3)");
    }

    /// <summary>
    /// A deleted guide gives its XP back, but not its place in the day: otherwise publishing and
    /// deleting would be a way past the cap. The cooldown it replaces read awards only for the same
    /// reason.
    /// </summary>
    [Fact]
    public async Task A_deleted_guide_still_counts_toward_the_day()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var first = await PublishGuideAsync("Getting a work permit");
        await PublishGuideAsync("Changing a driving licence");
        await PublishGuideAsync("Opening a business account");
        await Factory.DrainOutboxAsync();

        (await HttpClient.DeleteAsync($"{Guides}/{first}")).EnsureSuccessStatusCode();
        await Factory.DrainOutboxAsync();

        await PublishGuideAsync("Voting from abroad");
        await Factory.DrainOutboxAsync();

        (await GuideAwardsAsync(auth.User.Id)).Should().HaveCount(3, "the fourth guide is the day's fourth");
    }

    /// <summary>
    /// The day is the last 24 hours, not a calendar day. Three awards two hours ago fill it; three
    /// awards a day and an hour ago do not.
    /// </summary>
    [Theory]
    [InlineData(2, false)]
    [InlineData(25, true)]
    public async Task The_day_is_the_last_twenty_four_hours(int hoursAgo, bool pays)
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        await Factory.DrainOutboxAsync();

        await SeedGuideAwardsAsync(auth.User.Id, count: 3, at: DateTimeOffset.UtcNow.AddHours(-hoursAgo));

        await PublishGuideAsync("Sending money home");
        await Factory.DrainOutboxAsync();

        (await GuideAwardsAsync(auth.User.Id)).Should().HaveCount(pays ? 4 : 3);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task SeedGuideAwardsAsync(Guid userId, int count, DateTimeOffset at)
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var member = UserId.From(userId);

        await services.GetRequiredService<TrustProfileProvisioner>().GetOrCreateBothAsync(member);

        var profile = await services.GetRequiredService<IReputationProfileRepository>().GetByUserAsync(member);
        var rule = DefaultReputationRules.All.Single(item => item.ActionType == ReputationActions.GuidePublished);

        for (var i = 0; i < count; i++)
        {
            var guideId = Guid.NewGuid();

            profile!.Apply(
                rule.ToAward(), "guide", guideId.ToString(), "NG", at, ReputationAwardKey.GuidePublished(guideId));
        }

        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    private async Task<ReputationProfile> ProfileAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var profile = await scope.ServiceProvider
            .GetRequiredService<IReputationProfileRepository>()
            .GetByUserAsync(UserId.From(userId));

        return profile ?? ReputationProfile.Open(UserId.From(userId), DateTimeOffset.UtcNow);
    }

    private async Task<IReadOnlyList<ReputationEvent>> GuideAwardsAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var page = await scope.ServiceProvider
            .GetRequiredService<IReputationProfileRepository>()
            .ListEventsAsync(UserId.From(userId), new PaginationRequest { PageSize = 50 });

        return [.. page.Items.Where(item =>
            item.ActionType == ReputationActions.GuidePublished && !item.IsReversal)];
    }

    private async Task<Guid> PublishGuideAsync(string title)
    {
        using var created = await DataAsync(await HttpClient.PostAsJsonAsync(
            Guides, new { title, categoryKey = "immigration", language = "en", countryCode = "NG" }));
        var guideId = created.RootElement.GetProperty("data").GetProperty("guideId").GetGuid();

        using var updated = await DataAsync(await HttpClient.PutAsJsonAsync(
            $"{Guides}/{guideId}",
            new
            {
                title,
                countryCode = "NG",
                sections = new[]
                {
                    new
                    {
                        sectionId = Guid.NewGuid(),
                        title = "What to do",
                        order = 0,
                        blocks = new[]
                        {
                            new
                            {
                                blockId = Guid.NewGuid(),
                                kind = "Markdown",
                                order = 0,
                                content = "Bring a passport and proof of address."
                            }
                        }
                    }
                }
            }));

        using var published = await DataAsync(await HttpClient.PostAsJsonAsync(
            $"{Guides}/{guideId}/publish", new { revisionNotes = "First edition." }));

        return guideId;
    }

    private static async Task<JsonDocument> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            $"the request should have succeeded, but answered {(int)response.StatusCode}: {body}");

        return JsonDocument.Parse(body);
    }
}
