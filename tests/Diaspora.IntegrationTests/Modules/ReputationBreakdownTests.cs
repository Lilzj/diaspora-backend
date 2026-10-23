using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// <c>GET /trust/reputation/me</c> says what each dimension's score is made of.
/// <para>
/// The reputation screen built its rows by multiplying the profile's counters by figures of its
/// own: thirty a phone where the server pays twenty, forty a country where it pays thirty, minus
/// twenty a violation where it costs seventy-five. Content removed by moderation cost twenty-five
/// and had no counter, so the trust score moved with no row to say why. The rows now come from the
/// scoring history, through the real route, and each dimension's rows plus its adjustment are its
/// score.
/// </para>
/// <para>
/// Each test checks only the actions it scores, and the sum on every dimension. The member is
/// phone-verified only, so nothing else should have scored them, but nothing here relies on that.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class ReputationBreakdownTests : PooledIdentityTestBase
{
    public ReputationBreakdownTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task The_breakdown_is_the_points_actually_awarded()
    {
        var member = await MemberAsync();

        await ScoreAsync(member, ReputationActions.GovernmentIdVerified, ReputationAwardKey.Verification(
            ReputationActions.GovernmentIdVerified, Guid.NewGuid()));
        await ScoreAsync(member, ReputationActions.CountryVerified, ReputationAwardKey.Verification(
            ReputationActions.CountryVerified, Guid.NewGuid()), countryCode: "MU");

        var verification = Dimension(await ReputationAsync(), "verification");

        Line(verification, ReputationActions.GovernmentIdVerified)
            .Should().Be(new LineView(ReputationActions.GovernmentIdVerified, "Government ID verified", 1, 40));
        Line(verification, ReputationActions.CountryVerified)
            .Should().Be(new LineView(ReputationActions.CountryVerified, "Countries verified", 1, 30));
    }

    [Fact]
    public async Task A_penalty_that_floors_the_score_shows_as_an_adjustment()
    {
        var member = await MemberAsync();

        await ScoreAsync(member, ReputationActions.CommunityConfirmation, ReputationAwardKey.Confirmation(
            "update", Guid.NewGuid().ToString(), Guid.NewGuid()), countryCode: "MU");
        await ScoreAsync(member, ReputationActions.ViolationRecorded, ReputationAwardKey.Moderation(
            ReputationActions.ViolationRecorded, Guid.NewGuid()));

        var reputation = await ReputationAsync();
        var trust = Dimension(reputation, "trust");

        reputation.TrustScore.Should().Be(0, "a score is held at zero");
        trust.Score.Should().Be(0);
        Line(trust, ReputationActions.CommunityConfirmation).Points.Should().Be(5);
        Line(trust, ReputationActions.ViolationRecorded).Points.Should().Be(-75);
        trust.Adjustment.Should().Be(70, "the floor absorbed seventy of the seventy-five");
        trust.Lines[^1].ActionType.Should().Be(
            ReputationActions.ViolationRecorded, "penalties come last");
    }

    /// <summary>REP-04. The penalty with no counter, which no row used to account for.</summary>
    [Fact]
    public async Task Content_removed_appears_as_its_own_line()
    {
        var member = await MemberAsync();

        await ScoreAsync(member, ReputationActions.HelpfulVoteReceived, ReputationAwardKey.HelpfulVote(
            Guid.NewGuid(), Guid.NewGuid()), countryCode: "MU");
        await ScoreAsync(member, ReputationActions.ContentRemoved, ReputationAwardKey.Moderation(
            ReputationActions.ContentRemoved, Guid.NewGuid()));

        var trust = Dimension(await ReputationAsync(), "trust");

        Line(trust, ReputationActions.ContentRemoved)
            .Should().Be(new LineView(ReputationActions.ContentRemoved, "Content removed by moderation", 1, -25));
    }

    /// <summary>
    /// An award applied directly, with no rule behind it, as a verification handler may. Its points
    /// are in the score; the row must exist and have a name.
    /// </summary>
    [Fact]
    public async Task An_award_with_no_rule_is_named_after_its_action()
    {
        var member = await MemberAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ReputationService>().ApplyAwardAsync(
                member,
                ReputationAward.Of("LegacyGuideBonus", ReputationDimension.contribution, 15),
                ReputationAwardKey.Verification("LegacyGuideBonus", Guid.NewGuid()));

            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        var contribution = Dimension(await ReputationAsync(), "contribution");

        Line(contribution, "LegacyGuideBonus")
            .Should().Be(new LineView("LegacyGuideBonus", "Legacy guide bonus", 1, 15));
    }

    [Fact]
    public async Task Every_dimension_adds_up_to_its_score()
    {
        var member = await MemberAsync();

        await ScoreAsync(member, ReputationActions.CommentWritten, ReputationAwardKey.Comment(Guid.NewGuid()));
        await ScoreAsync(member, ReputationActions.CommentWritten, ReputationAwardKey.Comment(Guid.NewGuid()));
        await ScoreAsync(member, ReputationActions.ContentRemoved, ReputationAwardKey.Moderation(
            ReputationActions.ContentRemoved, Guid.NewGuid()));

        var reputation = await ReputationAsync();

        reputation.Breakdown.Select(dimension => dimension.Dimension)
            .Should().Equal("contribution", "verification", "trust");

        Dimension(reputation, "contribution").Score.Should().Be(reputation.ContributionScore);
        Dimension(reputation, "verification").Score.Should().Be(reputation.VerificationScore);
        Dimension(reputation, "trust").Score.Should().Be(reputation.TrustScore);

        reputation.Breakdown.Should().OnlyContain(dimension =>
            dimension.Lines.Sum(line => line.Points) + dimension.Adjustment == dimension.Score);

        Line(Dimension(reputation, "contribution"), ReputationActions.CommentWritten)
            .Should().Be(new LineView(ReputationActions.CommentWritten, "Comments written", 2, 4));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>A phone-verified member, signed in.</summary>
    private async Task<UserId> MemberAsync()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        return UserId.From(auth.User.Id);
    }

    private async Task ScoreAsync(
        UserId member, string actionType, ReputationAwardKey key, string? countryCode = null)
    {
        using var scope = Factory.Services.CreateScope();

        var outcome = await scope.ServiceProvider
            .GetRequiredService<ReputationService>()
            .ScoreAsync(member, actionType, key, "test", Guid.NewGuid().ToString(), countryCode);

        outcome.WasApplied.Should().BeTrue(outcome.SkipReason);

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    private async Task<ReputationView> ReputationAsync() =>
        await IdentityApiClient.ReadDataAsync<ReputationView>(
            await HttpClient.GetAsync("/api/v1/trust/reputation/me"));

    private static DimensionView Dimension(ReputationView reputation, string name) =>
        reputation.Breakdown.Single(dimension => dimension.Dimension == name);

    private static LineView Line(DimensionView dimension, string actionType) =>
        dimension.Lines.Single(line => line.ActionType == actionType);

    private sealed record ReputationView(
        int ContributionScore, int VerificationScore, int TrustScore, List<DimensionView> Breakdown);

    private sealed record DimensionView(string Dimension, int Score, List<LineView> Lines, int Adjustment);

    private sealed record LineView(string ActionType, string Label, int Count, int Points);
}
