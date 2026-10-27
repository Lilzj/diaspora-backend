using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Financial.Application.Consumers.Rewards;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Platform.Messaging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// A held member asks for a review in the app, and an approved appeal lifts the hold (RWD-14,
/// CS-4a).
/// <para>
/// End to end through the real routes: the platform holds the member (the shared-device signal, as
/// Identity announces it), the member files a <c>RewardHold</c> appeal against themselves, and a
/// moderator decides it. An approval lifts the hold in the moderator's name and, like any lift,
/// clears the shared-device flag; a rejection leaves the hold as it was.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class RewardHoldAppealTests : PooledIdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RewardHoldAppealTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task An_approved_appeal_lifts_the_hold_in_the_reviewers_name()
    {
        var (member, appealId) = await HeldMemberAppealsAsync();

        var (moderator, _) = await SignInAsRoleAsync("Moderator");
        (await ReviewAsync(appealId, "Approve")).EnsureSuccessStatusCode();
        await Factory.DrainOutboxAsync();
        await Factory.DrainOutboxAsync();

        using var scope = Factory.Services.CreateScope();
        var account = (await scope.ServiceProvider.GetRequiredService<IRewardAccountRepository>()
            .FindByMemberAsync(member))!;

        account.IsConversionBlocked.Should().BeFalse();
        account.HoldDecisions.OrderBy(decision => decision.DecidedAt).Last()
            .Should().BeEquivalentTo(new
            {
                IsHeld = false,
                Reason = AppealApprovedRewardHoldConsumer.Reason,
                DecidedByUserId = (Guid?)moderator
            });

        (await scope.ServiceProvider.GetRequiredService<ICreatorProfileRepository>().FindByUserAsync(member))!
            .IsSharedDeviceFlagged.Should().BeFalse("an approved appeal is a staff lift, and a lift is the review");
    }

    [Fact]
    public async Task A_rejected_appeal_leaves_the_hold_as_it_was()
    {
        var (member, appealId) = await HeldMemberAppealsAsync();

        await SignInAsRoleAsync("Moderator");
        (await ReviewAsync(appealId, "Reject")).EnsureSuccessStatusCode();
        await Factory.DrainOutboxAsync();

        using var scope = Factory.Services.CreateScope();
        var account = (await scope.ServiceProvider.GetRequiredService<IRewardAccountRepository>()
            .FindByMemberAsync(member))!;

        account.IsConversionBlocked.Should().BeTrue();
        account.HoldDecisions.Should().ContainSingle("the appeal itself keeps the rejection");
    }

    [Fact]
    public async Task A_member_cannot_appeal_somebody_elses_hold()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await FileAsync(Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("trust.appeal.not-allowed");
    }

    /// <summary>A member held by the platform, who has filed a review of their hold.</summary>
    private async Task<(Guid Member, Guid AppealId)> HeldMemberAppealsAsync()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        await Factory.DrainOutboxAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>().PublishAsync(
                new AccountDeviceSharedIntegrationEvent
                {
                    UserId = auth.User.Id,
                    OtherAccounts = 1,
                    DetectedAt = DateTimeOffset.UtcNow
                });
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        await Factory.DrainOutboxAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        var filed = await FileAsync(auth.User.Id);
        filed.StatusCode.Should().Be(HttpStatusCode.Created, await filed.Content.ReadAsStringAsync());

        var body = JsonDocument.Parse(await filed.Content.ReadAsStringAsync()).RootElement;

        return (auth.User.Id, body.GetProperty("data").GetProperty("appealId").GetGuid());
    }

    private Task<HttpResponseMessage> FileAsync(Guid targetUserId) =>
        HttpClient.PostAsJsonAsync(
            "/api/v1/trust/appeals",
            new
            {
                type = "RewardHold",
                targetId = targetUserId.ToString(),
                reason = "My points are paused. I share a phone with my sister; please review it."
            },
            JsonOptions);

    private Task<HttpResponseMessage> ReviewAsync(Guid appealId, string decision) =>
        HttpClient.PostAsJsonAsync(
            $"/api/v1/trust/appeals/{appealId}/review",
            new { decision, explanation = "Reviewed against the account's history." },
            JsonOptions);
}
