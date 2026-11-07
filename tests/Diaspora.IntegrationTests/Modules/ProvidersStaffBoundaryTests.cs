using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Staff act on a member's provider only through the staff routes.
/// <para>
/// <b>Until 7 Nov 2026 the provider owner check began with "if the caller is staff, allow".</b> A
/// moderator could rewrite any business's description, contacts, prices, photos, services and
/// hours, and move its status, through the owner's own routes. The console suspended providers
/// through the owner's status route with an optional reason. The owner's routes now refuse staff,
/// and staff suspend and reinstate through <c>POST /providers/moderation/{providerId}</c>, which
/// requires a reason.
/// </para>
/// <para>
/// Each refusal is asserted by its error code as well as its status, so a test cannot pass
/// because the endpoint's policy turned staff away before the owner check was ever asked.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class ProvidersStaffBoundaryTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/providers";

    private const string NotOwned = "providers.provider.not-owned";

    private const string SuspendedCode = "providers.provider.suspended";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ProvidersStaffBoundaryTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>An edit and a state change on the owner's routes: staff refused, the owner not.</summary>
    [Fact]
    public async Task Staff_cannot_edit_or_archive_a_members_provider_and_the_owner_still_can()
    {
        var provider = await ActiveProviderAsync("Kente Tailoring");

        await SignInAsRoleAsync("Moderator");

        var edited = await HttpClient.PatchAsJsonAsync(
            $"{Root}/{provider.ProviderId}/details", new { description = "Rewritten by staff." });
        edited.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await edited.Content.ReadAsStringAsync()).Should().Contain(NotOwned);

        var archived = await HttpClient.PutAsJsonAsync(
            $"{Root}/{provider.ProviderId}/status", new { status = "Archived", reason = "Staff." });
        archived.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await archived.Content.ReadAsStringAsync()).Should().Contain(NotOwned);

        (await StatusOfAsync(provider.ProviderId)).Should().Be("Active", "staff changed nothing");

        // The owner, on the same two routes.
        Api.Authenticate(provider.OwnerToken);

        var ownerEdited = await HttpClient.PatchAsJsonAsync(
            $"{Root}/{provider.ProviderId}/details",
            new { description = "Made-to-measure kente and ankara, fitted in Peckham." });
        ownerEdited.StatusCode.Should().Be(
            HttpStatusCode.OK, await ownerEdited.Content.ReadAsStringAsync());

        var ownerArchived = await HttpClient.PutAsJsonAsync(
            $"{Root}/{provider.ProviderId}/status", new { status = "Archived" });
        ownerArchived.StatusCode.Should().Be(
            HttpStatusCode.OK, await ownerArchived.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// The console's old way of suspending — the owner's status route — no longer works for staff.
    /// </summary>
    [Fact]
    public async Task Staff_cannot_suspend_through_the_owners_status_route()
    {
        var provider = await ActiveProviderAsync("Accra Phone Repairs");

        await SignInAsRoleAsync("Admin");

        var suspended = await HttpClient.PutAsJsonAsync(
            $"{Root}/{provider.ProviderId}/status",
            new { status = "Suspended", reason = "Reported for fake reviews." });

        suspended.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await suspended.Content.ReadAsStringAsync()).Should().Contain(NotOwned);
        (await StatusOfAsync(provider.ProviderId)).Should().Be("Active");
    }

    /// <summary>
    /// What replaces it: a moderator suspends and reinstates through the moderation route, and a
    /// reason is required every time. The suspension's reason is what the owner's team is shown.
    /// </summary>
    [Fact]
    public async Task A_moderator_suspends_and_reinstates_through_the_moderation_route_with_a_reason()
    {
        var provider = await ActiveProviderAsync("Lagos Cargo Shipping");

        // A member cannot use the staff route, even on their own business.
        var byOwner = await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/{provider.ProviderId}",
            new { action = "Reinstate", reason = "I would like to be reinstated." });
        byOwner.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await SignInAsRoleAsync("Moderator");

        var withoutReason = await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/{provider.ProviderId}", new { action = "Suspend" });
        withoutReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await withoutReason.Content.ReadAsStringAsync())
            .Should().Contain("providers.moderation.reason-required");

        var blankReason = await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/{provider.ProviderId}", new { action = "Suspend", reason = "   " });
        blankReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await StatusOfAsync(provider.ProviderId)).Should().Be("Active", "a refused decision changes nothing");

        var suspended = await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/{provider.ProviderId}",
            new { action = "Suspend", reason = "Three customers report goods never delivered." });
        suspended.StatusCode.Should().Be(HttpStatusCode.OK, await suspended.Content.ReadAsStringAsync());

        var afterSuspension = await ReadDataAsync<ProviderShape>(suspended);
        afterSuspension.Status.Should().Be("Suspended");
        afterSuspension.StatusReason.Should().Be("Three customers report goods never delivered.");

        var unknown = await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/{provider.ProviderId}", new { action = "Delete", reason = "No such verb." });
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var reinstated = await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/{provider.ProviderId}",
            new { action = "Reinstate", reason = "Deliveries confirmed by the customers." });
        reinstated.StatusCode.Should().Be(HttpStatusCode.OK, await reinstated.Content.ReadAsStringAsync());

        (await ReadDataAsync<ProviderShape>(reinstated)).Status.Should().Be("Active");

        // Reinstating what is not suspended is refused rather than answered with success.
        var again = await HttpClient.PostAsJsonAsync(
            $"{Root}/moderation/{provider.ProviderId}",
            new { action = "Reinstate", reason = "Twice." });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// The audit's escape: an archived provider may be published again, so an owner archived a
    /// suspended provider and then republished it, and was back in the directory without staff
    /// lifting anything. Archiving a suspended provider is now refused; only staff end a suspension.
    /// </summary>
    [Fact]
    public async Task An_owner_cannot_escape_a_suspension_by_archiving_and_republishing()
    {
        var provider = await ActiveProviderAsync("Brixton Money Transfer");

        await SignInAsRoleAsync("Moderator");

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/moderation/{provider.ProviderId}",
                new { action = "Suspend", reason = "Unlicensed money transmission." }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        Api.Authenticate(provider.OwnerToken);

        var archived = await HttpClient.PutAsJsonAsync(
            $"{Root}/{provider.ProviderId}/status", new { status = "Archived" });
        archived.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await archived.Content.ReadAsStringAsync()).Should().Contain(SuspendedCode);

        var republished = await HttpClient.PutAsJsonAsync(
            $"{Root}/{provider.ProviderId}/status", new { status = "Active" });
        republished.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await republished.Content.ReadAsStringAsync()).Should().Contain(SuspendedCode);

        var paused = await HttpClient.PutAsJsonAsync(
            $"{Root}/{provider.ProviderId}/status", new { status = "Paused" });
        paused.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var asOwner = await ReadDataAsync<ProviderShape>(
            await HttpClient.GetAsync($"{Root}/{provider.ProviderId}"));
        asOwner.Status.Should().Be("Suspended");
        asOwner.StatusReason.Should().Be("Unlicensed money transmission.", "the owner is told why");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private sealed record ActiveProvider(Guid ProviderId, string OwnerToken);

    /// <summary>A provider registered and published by a fresh owner, left signed in as them.</summary>
    private async Task<ActiveProvider> ActiveProviderAsync(string name)
    {
        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var registered = await HttpClient.PostAsJsonAsync(
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

        registered.IsSuccessStatusCode.Should().BeTrue(await registered.Content.ReadAsStringAsync());

        var providerId = (await ReadDataAsync<ProviderShape>(registered)).ProviderId;

        var published = await HttpClient.PutAsJsonAsync(
            $"{Root}/{providerId}/status", new { status = "Active" });

        published.StatusCode.Should().Be(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());

        return new ActiveProvider(providerId, owner.Tokens.AccessToken);
    }

    /// <summary>The provider's status as the signed-in caller reads it.</summary>
    private async Task<string> StatusOfAsync(Guid providerId) =>
        (await ReadDataAsync<ProviderShape>(await HttpClient.GetAsync($"{Root}/{providerId}"))).Status;

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions);

        return envelope!.Data!;
    }

    private sealed record Envelope<T>(T? Data);

    private sealed record ProviderShape(Guid ProviderId, string Status, string? StatusReason);
}
