using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// A member's profile photo and creator cover (20 Oct 2026). The app said "Photos are not stored
/// yet": nothing could make a member's own upload readable by anyone else, the only way to record
/// a photo was the seven-field profile PUT, and the creator projection's photo was only ever set
/// to null at registration.
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class MemberAvatarTests : PooledIdentityTestBase
{
    private const string Avatar = "/api/v1/auth/profile/avatar";
    private const string Cover = "/api/v1/creators/me/cover";

    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    public MemberAvatarTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_member_sets_a_photo_that_their_profile_their_creator_card_and_anyone_can_see()
    {
        await SignInAsync();
        var mediaId = await UploadAsync();

        var set = await HttpClient.PutAsJsonAsync(Avatar, new { mediaId });

        set.StatusCode.Should().Be(HttpStatusCode.OK, await set.Content.ReadAsStringAsync());

        var avatarUrl = await DataAsync(set, data => data.GetProperty("avatarUrl").GetString());

        avatarUrl.Should().EndWith($"/api/v1/media/{mediaId}/variants/medium");
        Uri.IsWellFormedUriString(avatarUrl, UriKind.Absolute).Should().BeTrue(
            "the app renders the address directly");

        var me = await Api.GetCurrentUserAsync();
        me.StatusCode.Should().Be(HttpStatusCode.OK);

        (await DataAsync(me, data => data.GetProperty("avatarUrl").GetString())).Should().Be(avatarUrl);
        (await DataAsync(me, data => data.GetProperty("avatarStorageKey").GetString()))
            .Should().Be(mediaId.ToString());

        await Factory.DrainOutboxAsync();

        var creator = await HttpClient.GetAsync("/api/v1/creators/me");
        creator.StatusCode.Should().Be(HttpStatusCode.OK, await creator.Content.ReadAsStringAsync());

        (await DataAsync(creator, data => data.GetProperty("avatarUrl").GetString()))
            .Should().Be(avatarUrl, "the creator card shows the member's photo");

        Api.ClearAuthentication();

        var anonymous = await HttpClient.GetAsync(new Uri(avatarUrl!).PathAndQuery);

        anonymous.StatusCode.Should().Be(HttpStatusCode.OK, "an image tag sends no bearer token");
        (await anonymous.Content.ReadAsByteArrayAsync()).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Someone_elses_upload_cannot_become_a_members_photo()
    {
        await SignInAsync();
        var someoneElses = await UploadAsync();

        await SignInAsync();

        var set = await HttpClient.PutAsJsonAsync(Avatar, new { mediaId = someoneElses });

        set.IsSuccessStatusCode.Should().BeFalse();

        var me = await Api.GetCurrentUserAsync();

        (await DataAsync(me, data => data.TryGetProperty("avatarUrl", out var url) ? url.GetString() : null))
            .Should().BeNull();

        Api.ClearAuthentication();

        (await HttpClient.GetAsync($"/api/v1/media/{someoneElses}/variants/medium"))
            .IsSuccessStatusCode.Should().BeFalse("a refused upload stays private");
    }

    [Fact]
    public async Task Removing_the_photo_clears_it_everywhere_and_makes_the_upload_private_again()
    {
        await SignInAsync();
        var mediaId = await UploadAsync();

        (await HttpClient.PutAsJsonAsync(Avatar, new { mediaId })).EnsureSuccessStatusCode();
        await Factory.DrainOutboxAsync();

        var removed = await HttpClient.DeleteAsync(Avatar);

        removed.StatusCode.Should().Be(HttpStatusCode.OK, await removed.Content.ReadAsStringAsync());
        (await DataAsync(removed, data => data.TryGetProperty("avatarUrl", out var url) ? url.GetString() : null))
            .Should().BeNull();

        await Factory.DrainOutboxAsync();

        var creator = await HttpClient.GetAsync("/api/v1/creators/me");
        (await DataAsync(creator, data => data.TryGetProperty("avatarUrl", out var url) ? url.GetString() : null))
            .Should().BeNull();

        Api.ClearAuthentication();

        (await HttpClient.GetAsync($"/api/v1/media/{mediaId}/variants/medium"))
            .IsSuccessStatusCode.Should().BeFalse("a removed photo is no longer public");
    }

    [Fact]
    public async Task A_creator_sets_a_cover_from_an_upload_that_anyone_can_see()
    {
        await SignInAsync();
        var mediaId = await UploadAsync();

        var set = await HttpClient.PutAsJsonAsync(Cover, new { mediaId });

        set.StatusCode.Should().Be(HttpStatusCode.OK, await set.Content.ReadAsStringAsync());

        var coverUrl = await DataAsync(set, data => data.GetProperty("coverImageUrl").GetString());
        coverUrl.Should().EndWith($"/api/v1/media/{mediaId}/variants/medium");

        var creator = await HttpClient.GetAsync("/api/v1/creators/me");
        (await DataAsync(creator, data => data.GetProperty("coverImageUrl").GetString())).Should().Be(coverUrl);

        Api.ClearAuthentication();

        (await HttpClient.GetAsync(new Uri(coverUrl!).PathAndQuery))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Someone_elses_upload_cannot_become_a_creators_cover()
    {
        await SignInAsync();
        var someoneElses = await UploadAsync();

        await SignInAsync();

        (await HttpClient.PutAsJsonAsync(Cover, new { mediaId = someoneElses }))
            .IsSuccessStatusCode.Should().BeFalse();

        var creator = await HttpClient.GetAsync("/api/v1/creators/me");
        (await DataAsync(creator, data => data.TryGetProperty("coverImageUrl", out var url) ? url.GetString() : null))
            .Should().BeNull();
    }

    [Fact]
    public async Task Replacing_the_cover_makes_the_old_one_private()
    {
        await SignInAsync();
        var first = await UploadAsync();
        var second = await UploadAsync();

        (await HttpClient.PutAsJsonAsync(Cover, new { mediaId = first })).EnsureSuccessStatusCode();
        (await HttpClient.PutAsJsonAsync(Cover, new { mediaId = second })).EnsureSuccessStatusCode();

        Api.ClearAuthentication();

        (await HttpClient.GetAsync($"/api/v1/media/{first}/variants/medium"))
            .IsSuccessStatusCode.Should().BeFalse();
        (await HttpClient.GetAsync($"/api/v1/media/{second}/variants/medium"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task SignInAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        // Registration opens the creator record through the outbox.
        await Factory.DrainOutboxAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
    }

    private static async Task<T> DataAsync<T>(HttpResponseMessage response, Func<JsonElement, T> read)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return read(document.RootElement.GetProperty("data"));
    }

    private async Task<Guid> UploadAsync()
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(OnePixelPng);

        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "pixel.png");
        form.Add(new StringContent("image"), "kind");

        var response = await HttpClient.PostAsync("/api/v1/media/uploads/direct", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        return await DataAsync(response, data =>
            (data.TryGetProperty("mediaId", out var mediaId) ? mediaId : data.GetProperty("id")).GetGuid());
    }
}
