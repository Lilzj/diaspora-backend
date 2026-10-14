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

    [Fact]
    public async Task An_owner_changes_the_description_contact_and_town_after_registering()
    {
        var (providerId, _) = await RegisterProviderAsync("Josh Kitchen");

        var patched = await HttpClient.PatchAsJsonAsync($"{Root}/{providerId}/details", new
        {
            description = "Home-cooked West African food, weekdays from noon.",
            email = "kitchen@example.com",
            phone = "",
            cityName = "Manchester"
        });

        patched.StatusCode.Should().Be(HttpStatusCode.OK, await patched.Content.ReadAsStringAsync());

        using var page = JsonDocument.Parse(await patched.Content.ReadAsStringAsync());
        var data = page.RootElement.GetProperty("data");

        data.GetProperty("profile").GetProperty("description").GetString()
            .Should().Be("Home-cooked West African food, weekdays from noon.");
        data.GetProperty("location").GetProperty("cityName").GetString().Should().Be("Manchester");

        var kinds = data.GetProperty("contactMethods").EnumerateArray()
            .Select(method => method.GetProperty("kind").GetString()).ToList();
        kinds.Should().Contain(kind => string.Equals(kind, "Email", StringComparison.OrdinalIgnoreCase));
        kinds.Should().NotContain(kind => string.Equals(kind, "Phone", StringComparison.OrdinalIgnoreCase),
            "an empty phone removes it");
    }

    [Fact]
    public async Task An_owner_removes_a_photo_and_the_next_one_becomes_the_cover()
    {
        var (providerId, _) = await RegisterProviderAsync("Two Photo Grill");
        var first = await UploadAsync();
        var second = await UploadAsync();

        await HttpClient.PostAsJsonAsync($"{Root}/{providerId}/photos", new { mediaId = first, role = "cover" });
        await HttpClient.PostAsJsonAsync($"{Root}/{providerId}/photos", new { mediaId = second, role = "gallery" });

        var removed = await HttpClient.DeleteAsync($"{Root}/{providerId}/photos/{first}");

        removed.StatusCode.Should().Be(HttpStatusCode.OK, await removed.Content.ReadAsStringAsync());

        using var page = JsonDocument.Parse(await removed.Content.ReadAsStringAsync());
        var data = page.RootElement.GetProperty("data");

        data.GetProperty("gallery").GetArrayLength().Should().Be(1);
        data.GetProperty("coverMedia").GetProperty("mediaId").GetGuid().Should().Be(second);
    }

    [Fact]
    public async Task Nobody_else_can_change_a_providers_details_or_photos()
    {
        var (providerId, _) = await RegisterProviderAsync("Owned Elsewhere");
        var photo = await UploadAsync();
        await HttpClient.PostAsJsonAsync($"{Root}/{providerId}/photos", new { mediaId = photo, role = "cover" });

        var (_, stranger) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(stranger.Tokens.AccessToken);

        (await HttpClient.PatchAsJsonAsync($"{Root}/{providerId}/details", new { description = "Mine now" }))
            .IsSuccessStatusCode.Should().BeFalse();
        (await HttpClient.DeleteAsync($"{Root}/{providerId}/photos/{photo}"))
            .IsSuccessStatusCode.Should().BeFalse();
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
