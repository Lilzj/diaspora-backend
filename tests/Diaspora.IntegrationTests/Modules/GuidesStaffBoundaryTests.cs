using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Staff never act on a member's guide through the author's own routes; they act only through
/// moderation.
/// <para>
/// <b>Until this suite, the author check began "if the caller is staff, allow".</b> A moderator or
/// an administrator could rewrite, publish, archive or delete any member's guide through the very
/// routes the author uses, with no report, no reason and no notice — and the guide's history would
/// record the change as the author's own kind of edit. The owner's rule (7 Nov 2026) is that there
/// are no support edits: staff take a guide off the shelves with <c>/hide</c>, put it back with
/// <c>/unhide</c>, note why with <c>/moderation-notes</c> and place it with <c>/featured</c>.
/// </para>
/// <para>
/// Each refusal is checked twice: by the status code, and by reading the guide back as its author,
/// so a test cannot pass because the route failed for some unrelated reason while the write went
/// through anyway.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class GuidesStaffBoundaryTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/guides";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public GuidesStaffBoundaryTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Staff are refused on the author's routes ────────────────────────────────────────────

    [Theory]
    [InlineData("Moderator")]
    [InlineData("Admin")]
    public async Task Staff_cannot_edit_a_members_guide(string role)
    {
        var (guideId, author) = await PublishedGuideAsync("Opening a bank account in Lagos");

        await SignInAsRoleAsync(role);

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/{guideId}",
            UpdatePayload("Rewritten by staff", "Staff decided this is what the guide says."),
            JsonOptions);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden, "only the author edits a guide; staff moderate it");

        Api.Authenticate(author);

        var guide = await ReadGuideAsync(guideId);

        guide.GetProperty("title").GetString().Should().Be(
            "Opening a bank account in Lagos", "the refused edit changed nothing");
    }

    [Theory]
    [InlineData("Moderator")]
    [InlineData("Admin")]
    public async Task Staff_cannot_archive_or_delete_a_members_guide(string role)
    {
        var (guideId, author) = await PublishedGuideAsync("Registering a business in Accra");

        await SignInAsRoleAsync(role);

        (await HttpClient.PostAsync($"{Root}/{guideId}/archive", content: null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "archiving is the author's act");

        (await HttpClient.DeleteAsync($"{Root}/{guideId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "deleting is the author's act");

        Api.Authenticate(author);

        var guide = await ReadGuideAsync(guideId);

        guide.GetProperty("status").GetString().Should().Be(
            "Published", "neither refused act moved the guide");
    }

    // ── The author is unaffected ────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_author_can_still_edit_and_archive_their_own_guide()
    {
        var (guideId, _) = await PublishedGuideAsync("Getting a tax number in Nairobi");

        var edited = await HttpClient.PutAsJsonAsync(
            $"{Root}/{guideId}",
            UpdatePayload("Getting a KRA PIN in Nairobi", "Apply online with your passport."),
            JsonOptions);

        edited.StatusCode.Should().Be(HttpStatusCode.OK, "the author edits their own guide");

        var archived = await HttpClient.PostAsync($"{Root}/{guideId}/archive", content: null);

        archived.StatusCode.Should().Be(HttpStatusCode.OK, "the author archives their own guide");

        var guide = await ReadGuideAsync(guideId);

        guide.GetProperty("title").GetString().Should().Be("Getting a KRA PIN in Nairobi");
        guide.GetProperty("status").GetString().Should().Be("Archived");
    }

    // ── Moderation still works ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_moderator_can_still_hide_and_unhide_a_members_guide()
    {
        var (guideId, _) = await PublishedGuideAsync("Sending money home from Dubai");

        await SignInAsRoleAsync("Moderator");

        var hidden = await DataAsync(await HttpClient.PostAsJsonAsync(
            $"{Root}/{guideId}/hide",
            new { reason = "The exchange-rate advice is out of date and misleading." },
            JsonOptions));

        hidden.GetProperty("moderationState").GetString().Should().Be("Hidden");

        (await HttpClient.GetAsync($"{Root}/moderation/queue?moderationState=Hidden"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the queue is still readable by staff");

        // A moderator still sees what they are deciding about, hidden or not.
        (await HttpClient.GetAsync($"{Root}/{guideId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "staff read visibility is unchanged");

        var restored = await DataAsync(
            await HttpClient.PostAsync($"{Root}/{guideId}/unhide", content: null));

        restored.GetProperty("moderationState").GetString().Should().Be("Clear");
    }

    /// <summary>
    /// A moderator's note is written (7 Nov 2026). Every note used to fail with 409: its key was
    /// treated as store-generated, so EF updated a row that did not exist instead of inserting it.
    /// </summary>
    [Fact]
    public async Task A_moderator_can_add_a_moderation_note_to_a_members_guide()
    {
        var (guideId, _) = await PublishedGuideAsync("Opening a bank account in Lisbon");

        await SignInAsRoleAsync("Moderator");

        var noted = await HttpClient.PostAsJsonAsync(
            $"{Root}/{guideId}/moderation-notes",
            new { note = "Checked the bank list against the regulator's register. Fine." },
            JsonOptions);

        noted.StatusCode.Should().Be(HttpStatusCode.OK, await noted.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_administrator_can_still_feature_a_members_guide()
    {
        var (guideId, _) = await PublishedGuideAsync("Finding a school place in Johannesburg");

        await SignInAsRoleAsync("Admin");

        (await HttpClient.PutAsync($"{Root}/{guideId}/featured", content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK, "featuring is curation, not authorship");

        (await HttpClient.DeleteAsync($"{Root}/{guideId}/featured"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A fresh verified member writes and publishes a guide. The caller is left signed in as that
    /// author, and the author's token is returned so a test can come back as them.
    /// </summary>
    private async Task<(Guid GuideId, string AuthorToken)> PublishedGuideAsync(string title)
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var created = await DataAsync(await HttpClient.PostAsJsonAsync(
            Root,
            new { title, categoryKey = "immigration", language = "en", countryCode = "NG" },
            JsonOptions));

        var guideId = created.GetProperty("guideId").GetGuid();

        (await HttpClient.PutAsJsonAsync(
                $"{Root}/{guideId}",
                UpdatePayload(title, "Bring a passport and proof of address."),
                JsonOptions))
            .EnsureSuccessStatusCode();

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/{guideId}/publish", new { revisionNotes = "First edition." }, JsonOptions))
            .EnsureSuccessStatusCode();

        return (guideId, auth.Tokens.AccessToken);
    }

    private static object UpdatePayload(string title, string content) =>
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
                        new { blockId = Guid.NewGuid(), kind = "Markdown", order = 0, content }
                    }
                }
            }
        };

    private async Task<JsonElement> ReadGuideAsync(Guid guideId) =>
        await DataAsync(await HttpClient.GetAsync($"{Root}/{guideId}"));

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} answered {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Clone();
    }
}
