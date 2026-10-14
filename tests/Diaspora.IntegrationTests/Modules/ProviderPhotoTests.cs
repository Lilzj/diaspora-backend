using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// A business's photographs reach its page (14 Oct 2026). There was no route to put a photograph
/// on a provider and no policy letting anyone but the uploader see one, so "Josh Kitchen" was
/// published with three uploads and no picture.
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class ProviderPhotoTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/providers";

    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ProviderPhotoTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task An_owners_photo_becomes_the_cover_and_other_members_can_see_it()
    {
        var (providerId, _) = await RegisterProviderAsync("Josh Kitchen");
        var mediaId = await UploadAsync();

        var attached = await HttpClient.PostAsJsonAsync(
            $"{Root}/{providerId}/photos", new { mediaId, role = "cover" });

        attached.StatusCode.Should().Be(HttpStatusCode.OK, await attached.Content.ReadAsStringAsync());

        (await HttpClient.PutAsJsonAsync($"{Root}/{providerId}/status", new { status = "Active" }))
            .IsSuccessStatusCode.Should().BeTrue();

        var (_, stranger) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(stranger.Tokens.AccessToken);

        using var page = JsonDocument.Parse(await HttpClient.GetStringAsync($"{Root}/{providerId}"));
        var data = page.RootElement.GetProperty("data");

        data.GetProperty("coverMedia").GetProperty("mediaId").GetGuid().Should().Be(mediaId);
        data.GetProperty("gallery").GetArrayLength().Should().Be(1);

        (await HttpClient.GetAsync($"/api/v1/media/{mediaId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_member_cannot_put_someone_elses_upload_on_their_business()
    {
        await RegisterProviderAsync("First Owner Bakery");
        var someoneElsesUpload = await UploadAsync();

        var (otherProviderId, _) = await RegisterProviderAsync("Second Owner Cafe");

        var attached = await HttpClient.PostAsJsonAsync(
            $"{Root}/{otherProviderId}/photos", new { mediaId = someoneElsesUpload, role = "gallery" });

        attached.IsSuccessStatusCode.Should().BeFalse();
    }

    private async Task<(Guid ProviderId, string Token)> RegisterProviderAsync(string name)
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            Root,
            new Dictionary<string, object?>
            {
                ["name"] = name,
                ["providerType"] = "Business",
                ["category"] = "Restaurants",
                ["cityName"] = "London",
                ["countryCode"] = "GB",
                ["phone"] = "+447700900456"
            },
            JsonOptions);

        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return (document.RootElement.GetProperty("data").GetProperty("providerId").GetGuid(),
            auth.Tokens.AccessToken);
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

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");

        return (data.TryGetProperty("mediaId", out var mediaId) ? mediaId : data.GetProperty("id")).GetGuid();
    }
}
