using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Community confirmations, over real HTTP.
/// <para>
/// Both halves of this contract had never run. <c>POST /trust/contributions/confirmations</c>
/// threw a 500 on an author's <em>first</em> confirmation — the provisioner added a
/// <c>ReputationProfile</c>, <c>ScoreAsync</c> queried the database, did not see the pending
/// insert, and added a second, which violated <c>ix_reputation_profiles_user</c> at save time.
/// Nothing read the result back either: <c>CountForTargetAsync</c> was written and never called
/// from anywhere. The table was empty in every environment, which is what a write path that
/// always throws looks like from the outside.
/// </para>
/// <para>
/// The wire shapes are re-declared by hand rather than referenced from the module, so these
/// cannot pass merely because the module compiles.
/// </para>
/// </summary>
[Collection(FinanceCollection.Name)]
public sealed class TrustConfirmationTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/trust/contributions/confirmations";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public TrustConfirmationTests(CustomWebApplicationFactory factory) : base(factory) { }

    private sealed record ConfirmationSummary(
        string TargetType, string TargetId, int ConfirmationCount, bool ConfirmedByViewer);

    private sealed record Envelope<T>(T Data);

    /// <summary>
    /// The regression. An author with no reputation profile is the ordinary case for a first
    /// confirmation, and it was the case that threw.
    /// </summary>
    [Fact]
    public async Task An_authors_first_ever_confirmation_is_recorded_rather_than_a_500()
    {
        var (_, author) = await RegisterVerifiedMemberAsync();
        var (_, confirmer) = await RegisterVerifiedMemberAsync();

        HttpClient.DefaultRequestHeaders.Authorization = Bearer(confirmer.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(Root, new
        {
            targetType = "guide",
            targetId = $"first-{Guid.NewGuid():N}",
            authorUserId = author.User.Id,
            countryCode = "NG"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_target_nobody_has_confirmed_reads_as_zero_rather_than_missing()
    {
        var (_, viewer) = await RegisterVerifiedMemberAsync();
        HttpClient.DefaultRequestHeaders.Authorization = Bearer(viewer.Tokens.AccessToken);

        var summary = await ReadSummaryAsync("guide", $"unconfirmed-{Guid.NewGuid():N}");

        // The confirm control has to render before the first confirmation exists, or nobody
        // can ever give it one.
        summary.ConfirmationCount.Should().Be(0);
        summary.ConfirmedByViewer.Should().BeFalse();
    }

    [Fact]
    public async Task A_recorded_confirmation_can_be_read_back()
    {
        var (_, author) = await RegisterVerifiedMemberAsync();
        var (_, confirmer) = await RegisterVerifiedMemberAsync();

        var targetId = $"readback-{Guid.NewGuid():N}";

        HttpClient.DefaultRequestHeaders.Authorization = Bearer(confirmer.Tokens.AccessToken);
        await ConfirmAsync(author.User.Id, targetId);

        var asConfirmer = await ReadSummaryAsync("guide", targetId);
        asConfirmer.ConfirmationCount.Should().Be(1);
        asConfirmer.ConfirmedByViewer.Should().BeTrue();

        // Another member sees the same tally and is not told they confirmed it.
        HttpClient.DefaultRequestHeaders.Authorization = Bearer(author.Tokens.AccessToken);

        var asAuthor = await ReadSummaryAsync("guide", targetId);
        asAuthor.ConfirmationCount.Should().Be(1);
        asAuthor.ConfirmedByViewer.Should().BeFalse();
    }

    /// <summary>
    /// The aggregate lower-cases the target type on write, so a read that did not would answer
    /// zero for a caller that spelled it with capitals.
    /// </summary>
    [Fact]
    public async Task The_target_type_matches_however_the_caller_spells_it()
    {
        var (_, author) = await RegisterVerifiedMemberAsync();
        var (_, confirmer) = await RegisterVerifiedMemberAsync();

        var targetId = $"casing-{Guid.NewGuid():N}";

        HttpClient.DefaultRequestHeaders.Authorization = Bearer(confirmer.Tokens.AccessToken);
        await ConfirmAsync(author.User.Id, targetId);

        (await ReadSummaryAsync("GUIDE", targetId)).ConfirmationCount.Should().Be(1);
    }

    /// <summary>
    /// One per member per target. Without it, confirming the same guide fifty times is a valid
    /// way to hand someone 250 trust points.
    /// </summary>
    [Fact]
    public async Task Confirming_the_same_thing_twice_is_refused_and_does_not_move_the_count()
    {
        var (_, author) = await RegisterVerifiedMemberAsync();
        var (_, confirmer) = await RegisterVerifiedMemberAsync();

        var targetId = $"duplicate-{Guid.NewGuid():N}";

        HttpClient.DefaultRequestHeaders.Authorization = Bearer(confirmer.Tokens.AccessToken);
        await ConfirmAsync(author.User.Id, targetId);

        var again = await HttpClient.PostAsJsonAsync(Root, new
        {
            targetType = "guide",
            targetId,
            authorUserId = author.User.Id,
            countryCode = "NG"
        });

        again.IsSuccessStatusCode.Should().BeFalse();
        (await ReadSummaryAsync("guide", targetId)).ConfirmationCount.Should().Be(1);
    }

    [Fact]
    public async Task Confirming_your_own_contribution_is_refused()
    {
        var (_, member) = await RegisterVerifiedMemberAsync();

        HttpClient.DefaultRequestHeaders.Authorization = Bearer(member.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(Root, new
        {
            targetType = "guide",
            targetId = $"self-{Guid.NewGuid():N}",
            authorUserId = member.User.Id,
            countryCode = "NG"
        });

        response.IsSuccessStatusCode.Should().BeFalse();
    }

    private static System.Net.Http.Headers.AuthenticationHeaderValue Bearer(string token) =>
        new("Bearer", token);

    private async Task ConfirmAsync(Guid authorUserId, string targetId)
    {
        var response = await HttpClient.PostAsJsonAsync(Root, new
        {
            targetType = "guide",
            targetId,
            authorUserId,
            countryCode = "NG"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private async Task<ConfirmationSummary> ReadSummaryAsync(string targetType, string targetId)
    {
        var response = await HttpClient.GetAsync(
            $"{Root}?targetType={Uri.EscapeDataString(targetType)}" +
            $"&targetId={Uri.EscapeDataString(targetId)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = JsonSerializer.Deserialize<Envelope<ConfirmationSummary>>(
            await response.Content.ReadAsStringAsync(), JsonOptions);

        envelope!.Data.Should().NotBeNull();
        return envelope.Data;
    }
}
