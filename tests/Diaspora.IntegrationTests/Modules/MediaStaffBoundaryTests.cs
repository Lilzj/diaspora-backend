using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Staff and a member's files, after 7 Nov 2026.
/// <para>
/// <b>Until then <c>media:administer</c> passed the management check outright.</b> A moderator
/// could edit, delete, re-attach or reorder any member's file through the member's own routes, with
/// no reason recorded and nothing to tell the member. The owner's rule is that staff never act on a
/// member's content through the member's routes, so those routes now answer a moderator as they
/// answer any other member — and an illegal or abusive file comes down through
/// <c>POST /media/{id}/moderation</c>, which takes a reason and keeps it on the file.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class MediaStaffBoundaryTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/media";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>A one-pixel PNG. Real bytes, so the signature check has something to read.</summary>
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    public MediaStaffBoundaryTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// Staff are refused on the member's own edit and delete routes, and the member is not.
    /// </summary>
    [Theory]
    [InlineData("Moderator")]
    [InlineData("Admin")]
    public async Task Staff_cannot_edit_or_delete_a_members_file_and_the_member_still_can(string role)
    {
        var (memberToken, mediaId) = await MemberUploadAsync();

        await SignInAsRoleAsync(role);

        (await HttpClient.PatchAsJsonAsync(
                $"{Root}/{mediaId}", new { caption = "Edited by staff" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await HttpClient.DeleteAsync($"{Root}/{mediaId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        ActAs(memberToken);

        (await HttpClient.PatchAsJsonAsync($"{Root}/{mediaId}", new { caption = "My own words" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await HttpClient.DeleteAsync($"{Root}/{mediaId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A moderator hides a file: nobody is served its bytes, the queue shows the reason, and the
    /// existing release route is how it comes back.
    /// </summary>
    [Fact]
    public async Task A_moderator_hides_a_members_file_with_a_reason_and_an_admin_can_release_it()
    {
        var (memberToken, mediaId) = await MemberUploadAsync();

        (await HttpClient.GetAsync($"{Root}/{mediaId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the uploader sees their own file");

        await SignInAsRoleAsync("Moderator");

        var hidden = await HttpClient.PostAsJsonAsync(
            $"{Root}/{mediaId}/moderation",
            new { action = "Hide", reason = "Graphic violence reported by two members." });

        hidden.StatusCode.Should().Be(HttpStatusCode.OK);

        var decision = await ReadDataAsync<ModerationDto>(hidden);

        decision.Status.Should().Be("quarantined");
        decision.Reason.Should().Be("Graphic violence reported by two members.");

        var queue = await ReadDataAsync<List<QuarantinedDto>>(
            await HttpClient.GetAsync($"{Root}/quarantine"));

        queue.Should().ContainSingle(row => row.MediaId == mediaId)
            .Which.Reason.Should().Be("Graphic violence reported by two members.");

        ActAs(memberToken);

        (await HttpClient.GetAsync($"{Root}/{mediaId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "a withheld file is served to nobody");

        await SignInAsRoleAsync("Admin");

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/{mediaId}/quarantine/release",
                new { reason = "Reviewed: news footage, not gratuitous." }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        ActAs(memberToken);

        (await HttpClient.GetAsync($"{Root}/{mediaId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A moderator removes a file outright, and the member can no longer read it.</summary>
    [Fact]
    public async Task A_moderator_removes_a_members_file_with_a_reason()
    {
        var (memberToken, mediaId) = await MemberUploadAsync();

        await SignInAsRoleAsync("Moderator");

        var removed = await HttpClient.PostAsJsonAsync(
            $"{Root}/{mediaId}/moderation",
            new { action = "Remove", reason = "Contains another member's passport number." });

        removed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDataAsync<ModerationDto>(removed)).Status.Should().Be("deleted");

        ActAs(memberToken);

        (await HttpClient.GetAsync($"{Root}/{mediaId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>The moderation route is staff's, and it does not move without a reason.</summary>
    [Fact]
    public async Task The_moderation_route_is_staff_only_and_needs_a_reason()
    {
        var (_, mediaId) = await MemberUploadAsync();

        // Another member: holds no staff permission.
        var (_, stranger) = await RegisterAndVerifyAsync();
        ActAs(stranger.Tokens.AccessToken);

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/{mediaId}/moderation", new { action = "Hide", reason = "Because." }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await SignInAsRoleAsync("Moderator");

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/{mediaId}/moderation", new { action = "Hide", reason = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/{mediaId}/moderation", new { action = "Shred", reason = "Because." }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>A verified member's upload, with the member signed in and their token kept.</summary>
    private async Task<(string Token, Guid MediaId)> MemberUploadAsync()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        ActAs(auth.Tokens.AccessToken);

        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(OnePixelPng);

        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "pixel.png");
        form.Add(new StringContent("image"), "kind");

        var uploaded = await HttpClient.PostAsync($"{Root}/uploads/direct", form);
        uploaded.StatusCode.Should().Be(HttpStatusCode.Created);

        return (auth.Tokens.AccessToken, (await ReadDataAsync<UploadedDto>(uploaded)).MediaId);
    }

    private void ActAs(string token)
    {
        Api.Authenticate(token);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions);

        return envelope!.Data!;
    }

    private sealed record Envelope<T>(T? Data);

    private sealed record UploadedDto(Guid MediaId);

    private sealed record ModerationDto(Guid MediaId, string Action, string Status, string Reason);

    private sealed record QuarantinedDto(Guid MediaId, string ScanStatus, string? Reason);
}
