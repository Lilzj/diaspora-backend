using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Staff never act on a member's event, series or organization through its owner's routes; they
/// act only through moderation.
/// <para>
/// <b>Until this suite, every owner check in community life began "if the caller is staff,
/// allow".</b> A moderator could rewrite, publish, cancel, archive or delete anybody's event or
/// organization, remove its members and hand out its Owner role — and a staff cancellation told
/// every attendee the organiser had called it off. The owner's rule (7 Nov 2026) is that there are
/// no support edits: staff hide, suspend or clear a subject through <c>/moderation</c>, decide
/// reports through <c>/reports</c>, and place subjects through <c>/featured</c>.
/// </para>
/// <para>
/// A member of staff who genuinely belongs to an organization is treated as what they are there:
/// a member with a role, and nothing more. Being staff no longer lifts the cap on which roles they
/// may grant.
/// </para>
/// </summary>
[Collection(CommunityCollection.Name)]
public sealed class CommunityLifeStaffBoundaryTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/community-life";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunityLifeStaffBoundaryTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Events ──────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Moderator")]
    [InlineData("Admin")]
    public async Task Staff_cannot_edit_or_cancel_a_members_event(string role)
    {
        var (eventId, organizer) = await PublishedEventAsync();

        await SignInAsRoleAsync(role);

        // An event a caller cannot manage answers not-found: a draft must not be confirmed to exist.
        (await HttpClient.PatchAsJsonAsync(
                $"{Root}/events/{eventId}", new { title = "Rewritten by staff" }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "only the organizer edits an event");

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/events/{eventId}/cancel", new { reason = "Staff called it off." }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "only the organizer cancels an event");

        Api.Authenticate(organizer);

        var read = await DataAsync(await HttpClient.GetAsync($"{Root}/events/{eventId}"));

        read.GetProperty("title").GetString().Should().StartWith(
            "Test Gathering", "the refused edit changed nothing");
        read.GetProperty("status").GetString().Should().Be(
            "Published", "the refused cancellation told nobody anything");
    }

    [Fact]
    public async Task The_organizer_can_still_edit_and_cancel_their_own_event()
    {
        var (eventId, _) = await PublishedEventAsync();

        (await HttpClient.PatchAsJsonAsync(
                $"{Root}/events/{eventId}", new { title = "Renamed by its organizer" }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/events/{eventId}/cancel", new { reason = "The hall flooded." }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var read = await DataAsync(await HttpClient.GetAsync($"{Root}/events/{eventId}"));

        read.GetProperty("title").GetString().Should().Be("Renamed by its organizer");
        read.GetProperty("status").GetString().Should().Be("Cancelled");
    }

    [Fact]
    public async Task Staff_can_still_feature_hide_and_read_a_members_event()
    {
        var (eventId, _) = await PublishedEventAsync();

        // Featuring is an administrator's permission; hiding is a moderator's.
        await SignInAsRoleAsync("Admin");

        (await HttpClient.PutAsJsonAsync(
                $"{Root}/events/{eventId}/featured", new { isFeatured = true }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK, "featuring is curation, not organizing");

        await SignInAsRoleAsync("Moderator");

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/events/{eventId}/moderation",
                new { action = "Hidden", reason = "Collecting deposits for a hall nobody booked." },
                JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the moderation route is how staff act");

        using (var anonymous = Factory.CreateClient())
        {
            (await anonymous.GetAsync($"{Root}/events/{eventId}"))
                .StatusCode.Should().Be(HttpStatusCode.NotFound, "the hide took effect");
        }

        (await HttpClient.GetAsync($"{Root}/events/{eventId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "a moderator still sees what they hid");
    }

    // ── Organizations ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Moderator")]
    [InlineData("Admin")]
    public async Task Staff_cannot_edit_or_archive_a_members_organization(string role)
    {
        var (organizationId, owner) = await OrganizationAsync();

        await SignInAsRoleAsync(role);

        (await HttpClient.PatchAsJsonAsync(
                $"{Root}/organizations/{organizationId}",
                new { summary = "Staff rewrote this page." },
                JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "only its administrators edit a page");

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/organizations/{organizationId}/archive",
                new { reason = "Staff filed it away." },
                JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "only its administrators archive it");

        Api.Authenticate(owner);

        var read = await DataAsync(
            await HttpClient.GetAsync($"{Root}/organizations/{organizationId}"));

        read.GetProperty("summary").GetString().Should().Be(
            "A body created by the integration suite.", "the refused edit changed nothing");
        read.GetProperty("status").GetString().Should().Be(
            "Draft", "the refused archive moved nothing");
    }

    [Fact]
    public async Task The_owner_can_still_edit_and_archive_their_own_organization()
    {
        var (organizationId, _) = await OrganizationAsync();

        (await HttpClient.PatchAsJsonAsync(
                $"{Root}/organizations/{organizationId}",
                new { summary = "Rewritten by its owner." },
                JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/organizations/{organizationId}/archive",
                new { reason = "We have merged with another body." },
                JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var read = await DataAsync(
            await HttpClient.GetAsync($"{Root}/organizations/{organizationId}"));

        read.GetProperty("summary").GetString().Should().Be("Rewritten by its owner.");
        read.GetProperty("status").GetString().Should().Be("Archived");
    }

    [Fact]
    public async Task Staff_can_still_feature_hide_and_read_a_members_organization()
    {
        var (organizationId, _) = await OrganizationAsync();

        // Featuring is an administrator's permission; hiding is a moderator's.
        await SignInAsRoleAsync("Admin");

        (await HttpClient.PutAsJsonAsync(
                $"{Root}/organizations/{organizationId}/featured",
                new { isFeatured = true },
                JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK, "featuring is curation, not administration");

        await SignInAsRoleAsync("Moderator");

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/organizations/{organizationId}/moderation",
                new { action = "Hidden", reason = "The page impersonates a registered charity." },
                JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the moderation route is how staff act");

        // A hidden, unpublished page used to resolve for staff only because staff passed the
        // owner check. It must still resolve for them now that they do not.
        var read = await DataAsync(
            await HttpClient.GetAsync($"{Root}/organizations/{organizationId}"));

        read.GetProperty("organizationId").GetGuid().Should().Be(organizationId);
    }

    // ── Memberships ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Staff_cannot_hand_out_the_owner_role_of_an_organization_they_do_not_belong_to()
    {
        var (organizationId, _) = await OrganizationAsync();

        var (_, target) = await RegisterVerifiedMemberAsync();

        await SignInAsRoleAsync("Admin");

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/organizations/{organizationId}/members/invitations",
                new { memberUserId = target.User.Id, role = "Owner" },
                JsonOptions))
            .StatusCode.Should().Be(
                HttpStatusCode.Forbidden, "staff are not members, and only members invite");
    }

    [Fact]
    public async Task Staff_who_belong_to_an_organization_are_capped_at_their_own_role_there()
    {
        var (organizationId, owner) = await OrganizationAsync();

        var (staffId, staff) = await SignInAsRoleAsync("Moderator");

        // The owner invites the member of staff as an ordinary moderator of the body, and they
        // accept: from here on they belong to it, with a role.
        Api.Authenticate(owner);

        var invitation = await DataAsync(await HttpClient.PostAsJsonAsync(
            $"{Root}/organizations/{organizationId}/members/invitations",
            new { memberUserId = staffId, role = "Moderator" },
            JsonOptions));

        var membershipId = invitation.GetProperty("membershipId").GetGuid();

        Api.Authenticate(staff.Tokens.AccessToken);

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/organizations/{organizationId}/members/{membershipId}/decision",
                new { approve = true },
                JsonOptions))
            .EnsureSuccessStatusCode();

        var (_, target) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(staff.Tokens.AccessToken);

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/organizations/{organizationId}/members/invitations",
                new { memberUserId = target.User.Id, role = "Owner" },
                JsonOptions))
            .StatusCode.Should().Be(
                HttpStatusCode.Forbidden, "a moderator of the body cannot manufacture an owner");

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/organizations/{organizationId}/members/invitations",
                new { memberUserId = target.User.Id, role = "Member" },
                JsonOptions))
            .StatusCode.Should().Be(
                HttpStatusCode.Created, "what their role in the body allows, they may still do");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A fresh verified member creates and publishes an event. The caller is left signed in as the
    /// organizer, and the organizer's token is returned so a test can come back as them.
    /// </summary>
    private async Task<(Guid EventId, string OrganizerToken)> PublishedEventAsync()
    {
        var (_, organizer) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(organizer.Tokens.AccessToken);

        var created = await DataAsync(await HttpClient.PostAsJsonAsync(
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
            },
            JsonOptions));

        var eventId = created.GetProperty("eventId").GetGuid();

        (await HttpClient.PostAsJsonAsync($"{Root}/events/{eventId}/publish", new { }, JsonOptions))
            .EnsureSuccessStatusCode();

        return (eventId, organizer.Tokens.AccessToken);
    }

    /// <summary>
    /// A fresh verified member creates an organization, which lands as a draft with them as its
    /// owner. The caller is left signed in as the owner.
    /// </summary>
    private async Task<(Guid OrganizationId, string OwnerToken)> OrganizationAsync()
    {
        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var created = await DataAsync(await HttpClient.PostAsJsonAsync(
            $"{Root}/organizations",
            new
            {
                name = $"Test Association {Guid.NewGuid():N}",
                summary = "A body created by the integration suite.",
                organizationTypeKey = "cultural-association",
                countryCode = "GB",
                cityName = "London"
            },
            JsonOptions));

        return (created.GetProperty("organizationId").GetGuid(), owner.Tokens.AccessToken);
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} answered {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Clone();
    }
}
