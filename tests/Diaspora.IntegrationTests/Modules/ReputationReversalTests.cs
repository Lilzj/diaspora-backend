using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Trust.Application.Consumers;
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
/// What content earned comes back with the content (D-9, REP-12), through the product.
/// <para>
/// The unit tests prove each consumer reverses what it is handed. These prove the half they
/// cannot: that deleting a guide or a comment through its own route reaches Trust at all, and
/// that the lookups by source and the counts net of reversals translate against Postgres.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class ReputationReversalTests : PooledIdentityTestBase
{
    private const string Guides = "/api/v1/guides";

    public ReputationReversalTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Deleting_a_guide_takes_back_its_award_once()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        await Factory.DrainOutboxAsync();

        var before = await ProfileAsync(auth.User.Id);
        var guideId = await PublishGuideAsync("Renewing a residence permit");
        await Factory.DrainOutboxAsync();

        var award = (await EventsAsync(auth.User.Id)).Single(item =>
            item.ActionType == ReputationActions.GuidePublished && !item.IsReversal);

        (await HttpClient.DeleteAsync($"{Guides}/{guideId}")).EnsureSuccessStatusCode();

        await Factory.DrainOutboxAsync();
        await Factory.DrainOutboxAsync();

        var reversal = (await EventsAsync(auth.User.Id)).Should()
            .ContainSingle(item => item.IsReversal, "a second drain reverses nothing").Subject;

        reversal.ReversesEventId.Should().Be(award.Id);
        reversal.Points.Should().Be(-award.Points);
        reversal.ActionType.Should().Be(ReputationActions.GuidePublished);

        var after = await ProfileAsync(auth.User.Id);
        after.ContributionScore.Should().Be(before.ContributionScore);
        after.GuidesPublished.Should().Be(0);

        using var scope = Factory.Services.CreateScope();
        var reputations = scope.ServiceProvider.GetRequiredService<IReputationProfileRepository>();

        (await reputations.GetRuleUsageAsync(UserId.From(auth.User.Id), ReputationActions.GuidePublished))
            .Total.Should().Be(0, "a deleted guide gives its place under the cap back");

        (await reputations.SummariseEventsAsync(after.Id))
            .Where(summary => summary.ActionType == ReputationActions.GuidePublished)
            .Sum(summary => summary.Count)
            .Should().Be(0, "the breakdown counts net of reversals");
    }

    /// <summary><c>CommunityCommentRemoved</c> names no author. Trust finds the award by the comment.</summary>
    [Fact]
    public async Task Deleting_a_comment_takes_back_its_award()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        using var post = await DataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new { body = "Is the passport office open on Saturday?", category = "Traffic", urgency = "Normal", publish = true }));
        var postId = post.RootElement.GetProperty("data").GetProperty("postId").GetGuid();

        using var comment = await DataAsync(await HttpClient.PostAsJsonAsync(
            $"/api/v1/community/posts/{postId}/comments", new { body = "Only until noon." }));
        var commentId = comment.RootElement.GetProperty("data").GetProperty("commentId").GetGuid();

        await Factory.DrainOutboxAsync();

        var award = (await EventsAsync(auth.User.Id)).Single(item =>
            item.ActionType == ReputationActions.CommentWritten && !item.IsReversal);
        award.SourceId.Should().Be(commentId.ToString());

        (await HttpClient.DeleteAsync($"/api/v1/community/comments/{commentId}")).EnsureSuccessStatusCode();
        await Factory.DrainOutboxAsync();

        (await EventsAsync(auth.User.Id)).Should()
            .ContainSingle(item => item.IsReversal)
            .Which.ReversesEventId.Should().Be(award.Id);

        (await ProfileAsync(auth.User.Id)).CommentsWritten.Should().Be(0);
    }

    /// <summary>Q4: an author retiring their own guide keeps what it earned.</summary>
    [Fact]
    public async Task An_author_archiving_their_own_guide_keeps_its_award()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var guideId = await PublishGuideAsync("Registering a car");
        await Factory.DrainOutboxAsync();

        (await HttpClient.PostAsJsonAsync($"{Guides}/{guideId}/archive", new { }))
            .EnsureSuccessStatusCode();
        await Factory.DrainOutboxAsync();

        var events = await EventsAsync(auth.User.Id);
        events.Should().Contain(item => item.ActionType == ReputationActions.GuidePublished);
        events.Should().NotContain(item => item.IsReversal);
    }

    /// <summary>
    /// An appeal's target is free text. Whether it names the content or the moderation item, the
    /// appellant's fine comes back, and only what it took.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_upheld_appeal_refunds_the_fine(bool namesTheContent)
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        var member = UserId.From(auth.User.Id);
        var contentId = Guid.NewGuid();
        var moderationItemId = Guid.NewGuid();
        await Factory.DrainOutboxAsync();

        int fined;

        using (var scope = Factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var reputation = services.GetRequiredService<ReputationService>();

            await services.GetRequiredService<TrustProfileProvisioner>().GetOrCreateBothAsync(member);
            await reputation.ApplyAwardAsync(
                member,
                ReputationAward.Of("Seed", ReputationDimension.trust, 100),
                ReputationAwardKey.Verification("Seed", Guid.NewGuid()));
            await reputation.ScoreAsync(
                member,
                ReputationActions.ContentRemoved,
                ReputationAwardKey.Moderation(ReputationActions.ContentRemoved, moderationItemId),
                "Post",
                contentId.ToString());
            await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

            fined = (await ProfileAsync(auth.User.Id)).TrustScore;
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var consumer = ActivatorUtilities.CreateInstance<AppealApprovedRefundConsumer>(scope.ServiceProvider);

            await consumer.Handle(
                new IntegrationEventNotification<AppealApprovedIntegrationEvent>(new AppealApprovedIntegrationEvent
                {
                    AppealId = Guid.NewGuid(),
                    UserId = auth.User.Id,
                    AppealType = "Content",
                    TargetId = (namesTheContent ? contentId : moderationItemId).ToString(),
                    DecidedAt = DateTimeOffset.UtcNow
                }),
                CancellationToken.None);
        }

        (await ProfileAsync(auth.User.Id)).TrustScore.Should().Be(fined + 25);
        (await EventsAsync(auth.User.Id)).Should().ContainSingle(item => item.IsReversal)
            .Which.Points.Should().Be(25);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<ReputationProfile> ProfileAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var profile = await scope.ServiceProvider
            .GetRequiredService<IReputationProfileRepository>()
            .GetByUserAsync(UserId.From(userId));

        return profile ?? ReputationProfile.Open(UserId.From(userId), DateTimeOffset.UtcNow);
    }

    private async Task<IReadOnlyList<ReputationEvent>> EventsAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var page = await scope.ServiceProvider
            .GetRequiredService<IReputationProfileRepository>()
            .ListEventsAsync(UserId.From(userId), new PaginationRequest { PageSize = 50 });

        return page.Items;
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
