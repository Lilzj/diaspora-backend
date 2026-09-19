using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Closing a report about an event, and the action a moderator asks for alongside it.
/// <para>
/// <b>The claim under test is that asking for something the platform cannot do is refused rather
/// than ignored.</b> The action field is parsed as a moderation state, so the natural verbs —
/// <c>Hide</c>, <c>Suspend</c> — do not parse. Until this suite existed the handler answered a word
/// it could not read with success: the report closed, the event stayed up, and the moderator was
/// told their decision had been recorded. The route next door, sharing the same parse, refused the
/// same word. One module, two answers.
/// </para>
/// <para>
/// The wire shapes are re-declared here rather than referenced from the module, for the reason the
/// neighbouring suite gives: a contract test bound to the module's own records passes whenever the
/// module compiles.
/// </para>
/// </summary>
public sealed class CommunityLifeModerationTests : IdentityTestBase
{
    private const string Root = "/api/v1/community-life";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunityLifeModerationTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// The defect itself: a verb the parser cannot read must not close a report quietly.
    /// </summary>
    [Fact]
    public async Task An_action_the_platform_cannot_read_is_refused_rather_than_ignored()
    {
        var (report, _) = await ReportedEventAsync();

        await AuthenticateAsModeratorAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/reports/{report.ReportId}/resolution",
            new { outcome = "Upheld", note = "Fraudulent.", action = "Suspend" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// And having refused, it must not have half-applied the decision: the report is still open for
    /// somebody to decide properly.
    /// </summary>
    [Fact]
    public async Task A_refused_action_leaves_the_report_open()
    {
        var (report, _) = await ReportedEventAsync();

        await AuthenticateAsModeratorAsync();

        await HttpClient.PostAsJsonAsync(
            $"{Root}/reports/{report.ReportId}/resolution",
            new { outcome = "Upheld", note = "Fraudulent.", action = "Suspend" });

        var queue = await ReadDataAsync<PageDto<ReportDto>>(
            await HttpClient.GetAsync($"{Root}/reports"));

        queue.Items.Should().Contain(entry => entry.ReportId == report.ReportId);
    }

    /// <summary>The state the parser does read is applied, and the event comes down.</summary>
    [Fact]
    public async Task The_state_the_platform_does_read_is_applied()
    {
        var (report, subject) = await ReportedEventAsync();

        await AuthenticateAsModeratorAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/reports/{report.ReportId}/resolution",
            new { outcome = "Upheld", note = "Fraudulent.", action = "Suspended" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Suspended means removed from discovery and barred from republication. Anonymous readers
        // are the surface that matters: the event was public a moment ago.
        using var anonymous = Factory.CreateClient();
        var read = await anonymous.GetAsync($"{Root}/events/{subject}");

        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Asking for nothing still works, and is not collateral damage from refusing the unreadable:
    /// dismissing a report without touching its subject is the commonest decision there is.
    /// </summary>
    [Fact]
    public async Task A_report_can_still_be_closed_without_touching_its_subject()
    {
        var (report, subject) = await ReportedEventAsync();

        await AuthenticateAsModeratorAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/reports/{report.ReportId}/resolution",
            new { outcome = "Dismissed", note = "Nothing wrong with it." });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var anonymous = Factory.CreateClient();
        var read = await anonymous.GetAsync($"{Root}/events/{subject}");

        read.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// <c>None</c> is documented as "close it and leave the subject alone", so it must keep meaning
    /// that rather than falling into the refusal with every other unparseable word.
    /// </summary>
    [Fact]
    public async Task None_still_means_leave_the_subject_alone()
    {
        var (report, subject) = await ReportedEventAsync();

        await AuthenticateAsModeratorAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/reports/{report.ReportId}/resolution",
            new { outcome = "Dismissed", note = "Looked at it.", action = "None" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var anonymous = Factory.CreateClient();
        (await anonymous.GetAsync($"{Root}/events/{subject}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// An ordinary member cannot close a report, however verified they are.
    /// <para>
    /// Signed in for real rather than with an assembled token: a token minted in the test process
    /// carries a session that does not exist and is refused at the door, which would make this pass
    /// without ever reaching the gate it claims to be about.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_ordinary_member_cannot_close_a_report()
    {
        var (report, _) = await ReportedEventAsync();

        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/reports/{report.ReportId}/resolution",
            new { outcome = "Dismissed", note = "Nothing wrong with it." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>An event somebody published, and somebody else reported.</summary>
    private async Task<(ReportDto Report, Guid EventId)> ReportedEventAsync()
    {
        var (_, organizer) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(organizer.Tokens.AccessToken);

        var published = await ReadDataAsync<EventDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/events",
                new
                {
                    title = $"Test Gathering {Guid.NewGuid():N}",
                    summary = "An event created by the integration suite.",
                    categoryKey = "community-meeting",
                    startsAt = DateTimeOffset.UtcNow.AddDays(14),
                    endsAt = DateTimeOffset.UtcNow.AddDays(14).AddHours(3),
                    countryCode = "GB",
                    cityName = "London",
                    venue = new { name = "Community Hall", addressLine1 = "1 High Street" }
                }));

        // Creating an event only drafts it, and a draft is a 404 to everybody but its organizer —
        // which would make every assertion below about an anonymous reader pass without moderation
        // having done anything at all.
        (await HttpClient.PostAsJsonAsync(
                $"{Root}/events/{published.EventId}/publish", new { }))
            .EnsureSuccessStatusCode();

        var (_, reporter) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reporter.Tokens.AccessToken);

        var filed = await ReadDataAsync<ReportDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/events/{published.EventId}/reports",
                new { reason = "Fraud", detail = "Collecting deposits for a hall nobody booked." }));

        return (filed, published.EventId);
    }

    /// <summary>A real Moderator: this module's gate reads the role, not a permission.</summary>
    private async Task AuthenticateAsModeratorAsync() => await SignInAsRoleAsync("Moderator");

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions);

        return envelope!.Data!;
    }

    private sealed record Envelope<T>(T? Data);

    private sealed record PageDto<T>(List<T> Items);

    private sealed record EventDto(Guid EventId, string Slug, string Status);

    private sealed record ReportDto(Guid ReportId, string SubjectType, Guid SubjectId, string Status);
}
