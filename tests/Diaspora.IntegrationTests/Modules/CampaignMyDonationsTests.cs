using System.Net;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// <c>GET /communities/campaigns/{id}/donations/me</c>: the caller's own donations to one campaign.
/// <para>
/// <b>Owner only.</b> Each test registers its own members and campaigns, and asserts on the
/// identifiers it created — never on how long somebody else's list is.
/// </para>
/// </summary>
public sealed class CampaignMyDonationsTests : CampaignTestBase<CustomWebApplicationFactory>
{
    public CampaignMyDonationsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task A_donor_reads_their_own_donations_to_a_campaign_with_the_campaign_around_them()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();
        campaign.CauseCode.Should().NotBeNullOrWhiteSpace("publishing creates the cause donations attribute to");

        var first = await DonateAsync(campaign.CauseCode!, 10m);
        var second = await DonateAsync(campaign.CauseCode!, 25m);

        var mine = await MyDonationsAsync(campaign.CampaignId);

        mine.Select(entry => entry.Donation.DonationId)
            .Should().BeEquivalentTo([first.DonationId, second.DonationId]);

        mine[0].Donation.DonationId.Should().Be(second.DonationId, "newest first");
        mine.Should().OnlyContain(entry =>
            entry.CampaignId == campaign.CampaignId
            && entry.CampaignTitle == campaign.Title
            && entry.Status == "Completed"
            && entry.Donation.CauseCode == campaign.CauseCode
            && entry.Payment != null);

        mine.Single(entry => entry.Donation.DonationId == second.DonationId)
            .Donation.Amount.Amount.Should().Be(25m);
    }

    /// <summary>
    /// Another member's donation to the same campaign is not in the caller's list, and the caller's
    /// is not in theirs. The route takes no user, so asking for somebody else's is not expressible.
    /// </summary>
    [Fact]
    public async Task Only_the_callers_own_donations_are_returned()
    {
        await MemberAsync();
        var campaign = await PublishedCampaignAsync();
        var organisers = await DonateAsync(campaign.CauseCode!, 5m);

        await MemberAsync();
        var supporters = await DonateAsync(campaign.CauseCode!, 7m);

        var mine = await MyDonationsAsync(campaign.CampaignId);

        mine.Should().ContainSingle().Which.Donation.DonationId.Should().Be(supporters.DonationId);
        mine.Should().NotContain(entry => entry.Donation.DonationId == organisers.DonationId);
    }

    /// <summary>
    /// An anonymous donation stores no donor, so it is not the caller's to list — the platform's
    /// anonymity rule, held here as everywhere else.
    /// </summary>
    [Fact]
    public async Task An_anonymous_donation_is_not_listed()
    {
        await MemberAsync();
        var campaign = await PublishedCampaignAsync();

        var named = await DonateAsync(campaign.CauseCode!, 5m);
        var anonymous = await DonateAsync(campaign.CauseCode!, 6m, isAnonymous: true);

        var mine = await MyDonationsAsync(campaign.CampaignId);

        mine.Should().Contain(entry => entry.Donation.DonationId == named.DonationId);
        mine.Should().NotContain(entry => entry.Donation.DonationId == anonymous.DonationId);
    }

    [Fact]
    public async Task A_member_who_has_not_given_reads_an_empty_list()
    {
        await MemberAsync();
        var campaign = await PublishedCampaignAsync();

        await MemberAsync();

        (await MyDonationsAsync(campaign.CampaignId)).Should().BeEmpty();
    }

    [Fact]
    public async Task An_unknown_campaign_is_not_found()
    {
        await MemberAsync();

        var response = await GetMyDonationsAsync(Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Somebody else's unpublished campaign is reported as absent, as the campaign route does.</summary>
    [Fact]
    public async Task Another_members_draft_campaign_is_not_found()
    {
        await MemberAsync();
        var draft = await CreateCampaignAsync();

        await MemberAsync();

        (await GetMyDonationsAsync(draft.CampaignId)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_anonymous_caller_is_refused()
    {
        await MemberAsync();
        var campaign = await PublishedCampaignAsync();

        HttpClient.DefaultRequestHeaders.Authorization = null;

        (await GetMyDonationsAsync(campaign.CampaignId)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private Task<HttpResponseMessage> GetMyDonationsAsync(Guid campaignId) =>
        HttpClient.GetAsync(new Uri($"{Communities}/campaigns/{campaignId}/donations/me", UriKind.Relative));

    private async Task<List<CampaignDonationShape>> MyDonationsAsync(Guid campaignId)
    {
        var response = await GetMyDonationsAsync(campaignId);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return await ReadDataAsync<List<CampaignDonationShape>>(response);
    }

    private sealed record DonationDetailShape(
        Guid DonationId, string? CauseCode, MoneyShape Amount, string Status, bool IsAnonymous);

    private sealed record CampaignDonationShape(
        DonationDetailShape Donation, Guid CampaignId, string CampaignTitle, string Status, PaymentShape? Payment);
}
