using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Messaging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// A referral, end to end (RWD-03, RWD-11).
/// <para>
/// Every piece existed and nothing joined them: registration took no code, nothing attributed a
/// signup, and a qualified referral was marked rewarded with no points behind it. Now the code travels
/// from registration to Creator's attribution, the invitee's first contribution qualifies it, Trust
/// pays the referrer 25 XP and 250 reward points, and the referrer's <c>first-referral</c> milestone
/// can be reached.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class ReferralRewardTests : PooledIdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ReferralRewardTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task An_invitees_first_contribution_pays_the_referrer_once()
    {
        var (_, referrer) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(referrer.Tokens.AccessToken);

        var code = (await DataAsync(await HttpClient.GetAsync("/api/v1/referrals/me")))
            .GetProperty("code").GetString()!;

        var (_, invitee) = await RegisterVerifiedMemberAsync(referralCode: code.ToLowerInvariant());
        await Factory.DrainOutboxAsync();

        var referral = await ReferralOfAsync(invitee.User.Id);
        referral.Should().NotBeNull("the registration carried the referrer's code");
        referral!.ReferrerUserId.Should().Be(referrer.User.Id);
        referral.State.Should().Be(ReferralState.Pending, "signing up on its own does not qualify");

        Api.Authenticate(invitee.Tokens.AccessToken);
        await PublishGuideAsync("Getting a national insurance number");
        await Factory.DrainOutboxAsync();
        await Factory.DrainOutboxAsync();

        (await ReferralOfAsync(invitee.User.Id))!.State.Should().Be(ReferralState.Rewarded);

        var award = (await EventsAsync(referrer.User.Id)).Should()
            .ContainSingle(item => item.ActionType == ReputationActions.ReferralQualified,
                "a redelivered qualification pays once").Subject;
        award.Points.Should().Be(25);

        var grant = (await RewardAccountAsync(referrer.User.Id)).Entries.Should()
            .ContainSingle(entry => entry.Source == ReputationActions.ReferralQualified).Subject;
        grant.Points.Should().Be(250);
        grant.Reference.Should().Be(ReputationAwardKey.Referral(referral.Id.Value).Value);

        Api.Authenticate(referrer.Tokens.AccessToken);
        var milestones = await DataAsync(await HttpClient.GetAsync("/api/v1/creators/me/milestones"));
        milestones.EnumerateArray()
            .Single(milestone => milestone.GetProperty("key").GetString() == "first-referral")
            .GetProperty("state").GetString()
            .Should().Be("Completed", "the qualified referral is the referrer's activity");
    }

    /// <summary>
    /// An invitee on a device another account already uses (C3.3, D-13). Their first contribution
    /// does not qualify the referral: it waits for review, and the referrer is told it is being
    /// checked and nothing about why. Staff lifting the invitee's hold, from the review queue, is the
    /// review, and the referral then qualifies and pays.
    /// </summary>
    [Fact]
    public async Task A_referral_from_a_shared_device_waits_for_review_and_qualifies_once_staff_clear_it()
    {
        var (_, referrer) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(referrer.Tokens.AccessToken);

        var code = (await DataAsync(await HttpClient.GetAsync("/api/v1/referrals/me")))
            .GetProperty("code").GetString()!;

        var (_, invitee) = await RegisterVerifiedMemberAsync(referralCode: code);
        await Factory.DrainOutboxAsync();
        await AnnounceSharedDeviceAsync(invitee.User.Id);

        Api.Authenticate(invitee.Tokens.AccessToken);
        await PublishGuideAsync("Opening a bank account");
        await Factory.DrainOutboxAsync();
        await Factory.DrainOutboxAsync();

        var waiting = (await ReferralOfAsync(invitee.User.Id))!;
        waiting.State.Should().Be(ReferralState.Pending, "a contribution from a shared device is not enough");
        waiting.StateReason.Should().Be(ReferralQualificationPolicy.ReviewReason);
        (await EventsAsync(referrer.User.Id)).Should()
            .NotContain(item => item.ActionType == ReputationActions.ReferralQualified);

        await SignInAsRoleAsync("Admin");
        (await HttpClient.PutAsJsonAsync(
                new Uri($"/api/v1/financial/operations/rewards/members/{invitee.User.Id}/conversion-hold", UriKind.Relative),
                new { isHeld = false, reason = "A family phone; reviewed." }))
            .EnsureSuccessStatusCode();
        await Factory.DrainOutboxAsync();
        await Factory.DrainOutboxAsync();

        (await ReferralOfAsync(invitee.User.Id))!.State.Should().Be(ReferralState.Rewarded);
        (await EventsAsync(referrer.User.Id)).Should()
            .ContainSingle(item => item.ActionType == ReputationActions.ReferralQualified);
    }

    /// <summary>A code nobody owns registers the account and credits nobody, silently.</summary>
    [Fact]
    public async Task An_unknown_code_registers_the_account_and_refers_nobody()
    {
        var (_, member) = await RegisterVerifiedMemberAsync(referralCode: "NOSUCHCODE1");
        await Factory.DrainOutboxAsync();

        (await ReferralOfAsync(member.User.Id)).Should().BeNull();
    }

    [Fact]
    public async Task A_malformed_code_is_refused_at_registration()
    {
        var response = await Api.RegisterAsync(RegistrationFor(NextPhoneNumber()) with { ReferralCode = "not a code!" });

        ((int)response.StatusCode).Should().Be(400);
        (await response.Content.ReadAsStringAsync()).Should().Contain("letters and numbers");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Identity's shared-device announcement, through the outbox as Identity publishes it.</summary>
    private async Task AnnounceSharedDeviceAsync(Guid userId)
    {
        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>().PublishAsync(
                new AccountDeviceSharedIntegrationEvent
                {
                    UserId = userId,
                    OtherAccounts = 1,
                    DetectedAt = DateTimeOffset.UtcNow
                });

            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        await Factory.DrainOutboxAsync();
    }

    private async Task<Diaspora.Modules.Creator.Domain.Referrals.CreatorReferral?> ReferralOfAsync(Guid inviteeUserId)
    {
        using var scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<ICreatorReferralRepository>()
            .FindByInviteeAsync(inviteeUserId);
    }

    private async Task<IReadOnlyList<ReputationEvent>> EventsAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var page = await scope.ServiceProvider
            .GetRequiredService<IReputationProfileRepository>()
            .ListEventsAsync(UserId.From(userId), new PaginationRequest { PageSize = 50 });

        return page.Items;
    }

    private async Task<RewardAccount> RewardAccountAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var account = await scope.ServiceProvider
            .GetRequiredService<IRewardAccountRepository>()
            .FindByMemberAsync(userId);

        account.Should().NotBeNull("the referrer's grant opens the account");

        return account!;
    }

    private async Task PublishGuideAsync(string title)
    {
        var guideId = (await DataAsync(await HttpClient.PostAsJsonAsync(
                "/api/v1/guides", new { title, categoryKey = "immigration", language = "en", countryCode = "NG" })))
            .GetProperty("guideId").GetGuid();

        await DataAsync(await HttpClient.PutAsJsonAsync(
            $"/api/v1/guides/{guideId}",
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
                                content = "Book an appointment and bring your passport."
                            }
                        }
                    }
                }
            }));

        await DataAsync(await HttpClient.PostAsJsonAsync(
            $"/api/v1/guides/{guideId}/publish", new { revisionNotes = "First edition." }));
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            $"the request should have succeeded, but answered {(int)response.StatusCode}: {body}");

        return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
    }
}
