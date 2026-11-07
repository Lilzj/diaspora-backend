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
/// answer any other member.
/// </para>
/// <para>
/// <b>Since stage 5 a file comes down only through a Trust moderation case.</b> The stage-2
/// <c>POST /media/{id}/moderation</c> is gone: a decision on the case withholds the file (a hide and
/// a removal alike, never a deletion, so an appeal can bring it back), and a restore on the case is
/// how it returns. The release route stays for what it was built for, a scanner's false positive,
/// and refuses a file a decision withheld.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class MediaStaffBoundaryTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/media";
    private const string Trust = "/api/v1/trust";

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
    /// A moderator hides a file by deciding a case: nobody is served its bytes, the quarantine queue
    /// shows the moderator's note, the release route cannot undo the decision, and a restore on the
    /// case, after the uploader asks for a review, brings it back.
    /// </summary>
    [Fact]
    public async Task A_moderator_hides_a_members_file_through_a_case_and_a_restore_brings_it_back()
    {
        var (memberToken, mediaId) = await MemberUploadAsync();

        (await HttpClient.GetAsync($"{Root}/{mediaId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the uploader sees their own file");

        await SignInAsRoleAsync("Moderator");

        var item = await OpenCaseAsync(mediaId, "OffensiveOrHarassment", "Graphic violence reported by two members.");
        await AssignAsync(item.ItemId);

        var hidden = await DecideAsync(
            item.ItemId, "HidePendingReview", "OffensiveOrHarassment", "Graphic violence reported by two members.");
        hidden.StatusCode.Should().Be(HttpStatusCode.OK, await hidden.Content.ReadAsStringAsync());

        var queue = await ReadDataAsync<List<QuarantinedDto>>(
            await HttpClient.GetAsync($"{Root}/quarantine?limit=200"));

        queue.Should().ContainSingle(row => row.MediaId == mediaId)
            .Which.Reason.Should().Be("Graphic violence reported by two members.");

        ActAs(memberToken);

        (await HttpClient.GetAsync($"{Root}/{mediaId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "a withheld file is served to nobody");

        // The release route overrules a scanner, not a moderation decision.
        await SignInAsRoleAsync("Admin");

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/{mediaId}/quarantine/release",
                new { reason = "Reviewed: news footage, not gratuitous." }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        ActAs(memberToken);

        (await HttpClient.GetAsync($"{Root}/{mediaId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await HttpClient.PostAsync($"{Trust}/moderation/cases/{item.ItemId}/review-request", content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await SignInAsRoleAsync("Moderator");
        await AssignAsync(item.ItemId);

        var restored = await DecideAsync(
            item.ItemId, "Restore", "Other", "Reviewed: news footage, not gratuitous.");
        restored.StatusCode.Should().Be(HttpStatusCode.OK, await restored.Content.ReadAsStringAsync());

        ActAs(memberToken);

        (await HttpClient.GetAsync($"{Root}/{mediaId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// <b>Changed by design (stage 5).</b> A removal used to delete the file. It is now a proposal a
    /// second member of staff confirms, and it withholds the file rather than deleting it: the member
    /// can no longer read it, and it is still there for an appeal to bring back.
    /// </summary>
    [Fact]
    public async Task A_removal_confirmed_by_a_second_member_of_staff_withholds_the_file_and_does_not_delete_it()
    {
        var (memberToken, mediaId) = await MemberUploadAsync();

        await SignInAsRoleAsync("Moderator");

        var item = await OpenCaseAsync(mediaId, "PersonalData", "Contains another member's passport number.");
        await AssignAsync(item.ItemId);

        var proposed = await DecideAsync(
            item.ItemId, "Remove", "PersonalData", "Contains another member's passport number.");
        proposed.StatusCode.Should().Be(HttpStatusCode.OK, await proposed.Content.ReadAsStringAsync());

        var pending = await ReadDataAsync<CaseDto>(proposed);
        pending.CurrentOutcome.Should().Be("HidePendingReview", "until it is confirmed, it is only hidden");
        pending.SecondReview.Should().Be("RemovalProposal");

        await SignInAsRoleAsync("Moderator");

        var confirmed = await HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/queue/{item.ItemId}/second-review",
            new { confirm = true, note = "Checked the image: the number is legible. Agreed." });
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());

        (await ReadDataAsync<CaseDto>(confirmed)).CurrentOutcome.Should().Be("Remove");

        // Withheld, not deleted: it is still in the queue, with the reason the uploader was given.
        var queue = await ReadDataAsync<List<QuarantinedDto>>(
            await HttpClient.GetAsync($"{Root}/quarantine?limit=200"));

        queue.Should().ContainSingle(row => row.MediaId == mediaId)
            .Which.Reason.Should().Be("Contains another member's passport number.");

        ActAs(memberToken);

        (await HttpClient.GetAsync($"{Root}/{mediaId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// <b>Changed by design (stage 5).</b> This asserted that the stage-2 moderation route was
    /// staff's and needed a reason. The route is gone; the same rules hold on the Trust case.
    /// </summary>
    [Fact]
    public async Task A_file_is_taken_down_only_through_a_case_which_needs_staff_and_a_note()
    {
        var (_, mediaId) = await MemberUploadAsync();

        // Another member: holds no staff permission.
        var (_, stranger) = await RegisterAndVerifyAsync();
        ActAs(stranger.Tokens.AccessToken);

        (await HttpClient.PostAsJsonAsync(
                $"{Trust}/moderation/cases",
                new { contentType = "media", contentId = mediaId, reasonCode = "Spam", note = "I do not like this file." }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await SignInAsRoleAsync("Moderator");

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/{mediaId}/moderation", new { action = "Hide", reason = "Graphic violence reported." }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);

        (await HttpClient.PostAsJsonAsync(
                $"{Trust}/moderation/cases",
                new { contentType = "media", contentId = mediaId, reasonCode = "Spam", note = "Because." }))
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

    private async Task<CaseDto> OpenCaseAsync(Guid mediaId, string reasonCode, string note)
    {
        var opened = await HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/cases",
            new { contentType = "media", contentId = mediaId, reasonCode, note });

        opened.StatusCode.Should().Be(HttpStatusCode.Created, await opened.Content.ReadAsStringAsync());

        return await ReadDataAsync<CaseDto>(opened);
    }

    private async Task AssignAsync(Guid itemId) =>
        (await HttpClient.PostAsync($"{Trust}/moderation/queue/{itemId}/assign", content: null))
            .EnsureSuccessStatusCode();

    private Task<HttpResponseMessage> DecideAsync(Guid itemId, string outcome, string reasonCode, string note) =>
        HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/queue/{itemId}/decision", new { outcome, reasonCode, note });

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

    private sealed record CaseDto(Guid ItemId, string? CurrentOutcome, string SecondReview);

    private sealed record QuarantinedDto(Guid MediaId, string ScanStatus, string? Reason);
}
