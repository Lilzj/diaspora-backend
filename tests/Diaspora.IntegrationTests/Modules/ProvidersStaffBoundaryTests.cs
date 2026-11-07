using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Staff act on a member's provider only by deciding its Trust moderation case.
/// <para>
/// <b>Until 7 Nov 2026 the provider owner check began with "if the caller is staff, allow".</b> A
/// moderator could rewrite any business's description, contacts, prices, photos, services and
/// hours, and move its status, through the owner's own routes. The console suspended providers
/// through the owner's status route with an optional reason. The owner's routes now refuse staff.
/// Stage 2 gave staff <c>POST /providers/moderation/{providerId}</c> to suspend and reinstate with a
/// reason; stage 5 removed it, and staff now suspend and reinstate a provider only by deciding its
/// Trust case, with a reason code and a note the owner's team is shown.
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

    private const string Trust = "/api/v1/trust";

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
    /// What replaces it: a moderator suspends and reinstates through the provider's Trust case, with
    /// a note every time. The note is the provider's status reason, which its team is shown. The
    /// stage 2 staff route is gone.
    /// </summary>
    [Fact]
    public async Task A_moderator_suspends_and_reinstates_through_the_trust_case_with_a_note()
    {
        var provider = await ActiveProviderAsync("Lagos Cargo Shipping");

        // A member cannot open a staff case, even on their own business.
        var byOwner = await HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/cases",
            new
            {
                contentType = "provider",
                contentId = provider.ProviderId,
                reasonCode = "Other",
                note = "I would like to open a case on myself."
            });
        byOwner.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await SignInAsRoleAsync("Moderator");

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/moderation/{provider.ProviderId}",
                new { action = "Suspend", reason = "The stage 2 staff route." }))
            .StatusCode.Should().BeOneOf(
                new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed }, "the old staff route is gone");

        var item = await OpenCaseAsync(provider.ProviderId, "FraudOrScam",
            "Three customers report goods paid for and never delivered.");
        await AssignAsync(item.ItemId);

        var tooShort = await DecideAsync(item.ItemId, "HidePendingReview", "FraudOrScam", "Scam.");
        tooShort.StatusCode.Should().Be(HttpStatusCode.BadRequest, "a note the owner can read is required");

        var unknown = await DecideAsync(item.ItemId, "Delete", "FraudOrScam", "Not an outcome the platform has.");
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await StatusOfAsync(provider.ProviderId)).Should().Be("Active", "a refused decision changes nothing");

        var suspended = await DecideAsync(item.ItemId, "HidePendingReview", "FraudOrScam",
            "Three customers report goods never delivered.");
        suspended.StatusCode.Should().Be(HttpStatusCode.OK, await suspended.Content.ReadAsStringAsync());

        var afterSuspension = await ReadDataAsync<ProviderShape>(
            await HttpClient.GetAsync($"{Root}/{provider.ProviderId}"));
        afterSuspension.Status.Should().Be("Suspended");
        afterSuspension.StatusReason.Should().Be("Three customers report goods never delivered.");

        using (var anonymous = Factory.CreateClient())
        {
            (await anonymous.GetAsync($"{Root}/{provider.ProviderId}")).IsSuccessStatusCode
                .Should().BeFalse("a suspended provider is off the public directory");
        }

        // The owner asks for another look; a second moderator restores it.
        Api.Authenticate(provider.OwnerToken);
        (await HttpClient.PostAsync($"{Trust}/moderation/cases/{item.ItemId}/review-request", content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK, "the provider's primary owner is the case's owner");

        await SignInAsRoleAsync("Moderator");
        await AssignAsync(item.ItemId);

        var reinstated = await DecideAsync(item.ItemId, "Restore", "Other",
            "Deliveries confirmed by the customers.");
        reinstated.StatusCode.Should().Be(HttpStatusCode.OK, await reinstated.Content.ReadAsStringAsync());

        var afterRestore = await ReadDataAsync<ProviderShape>(
            await HttpClient.GetAsync($"{Root}/{provider.ProviderId}"));
        afterRestore.Status.Should().Be("Active");
        afterRestore.StatusReason.Should().BeNull("the suspension it explained is over");

        // A case is decided once: restoring again is refused rather than answered with success.
        (await DecideAsync(item.ItemId, "Restore", "Other", "Restoring it a second time."))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// A removal is a proposal until a second member of staff confirms it; the provider is
    /// suspended throughout, and stays so with the case's outcome a removal.
    /// </summary>
    [Fact]
    public async Task A_removal_takes_effect_only_when_a_second_member_of_staff_confirms_it()
    {
        var provider = await ActiveProviderAsync("Peckham Visa Advice");

        await SignInAsRoleAsync("Moderator");

        var item = await OpenCaseAsync(provider.ProviderId, "AsksForFees",
            "Charges a fee up front for visa applications it never files.");
        await AssignAsync(item.ItemId);

        var proposed = await DecideAsync(item.ItemId, "Remove", "AsksForFees",
            "Takes fees up front for applications it never files.");
        proposed.StatusCode.Should().Be(HttpStatusCode.OK, await proposed.Content.ReadAsStringAsync());
        (await ReadDataAsync<CaseShape>(proposed)).CurrentOutcome.Should().Be("HidePendingReview");

        (await StatusOfAsync(provider.ProviderId)).Should().Be("Suspended", "suspended while the removal waits");

        (await SecondReviewAsync(item.ItemId, confirm: true, "Agreeing with my own proposal, obviously."))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "nobody checks their own decision");

        await SignInAsRoleAsync("Moderator");

        var confirmed = await SecondReviewAsync(item.ItemId, confirm: true,
            "Two clients sent receipts and the Home Office has no applications.");
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());
        (await ReadDataAsync<CaseShape>(confirmed)).CurrentOutcome.Should().Be("Remove");

        (await StatusOfAsync(provider.ProviderId)).Should().Be("Suspended", "a removal is the suspension that stands");
    }

    /// <summary>
    /// The audit's escape: an archived provider may be published again, so an owner archived a
    /// suspended provider and then republished it, and was back in the directory without staff
    /// lifting anything. Archiving a suspended provider is refused; only staff end a suspension,
    /// and since stage 5 the suspension comes from a decision on the Trust case.
    /// </summary>
    [Fact]
    public async Task An_owner_cannot_escape_a_suspension_by_archiving_and_republishing()
    {
        var provider = await ActiveProviderAsync("Brixton Money Transfer");

        await SignInAsRoleAsync("Moderator");

        var item = await OpenCaseAsync(provider.ProviderId, "FraudOrScam",
            "Moves money abroad without a money transmission licence.");
        await AssignAsync(item.ItemId);

        (await DecideAsync(item.ItemId, "HidePendingReview", "FraudOrScam", "Unlicensed money transmission."))
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

    /// <summary>The signed-in moderator opens a case on the provider with no report.</summary>
    private async Task<CaseShape> OpenCaseAsync(Guid providerId, string reasonCode, string note)
    {
        var opened = await HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/cases",
            new { contentType = "provider", contentId = providerId, reasonCode, note });

        opened.StatusCode.Should().Be(HttpStatusCode.Created, await opened.Content.ReadAsStringAsync());

        return await ReadDataAsync<CaseShape>(opened);
    }

    private async Task AssignAsync(Guid itemId) =>
        (await HttpClient.PostAsync($"{Trust}/moderation/queue/{itemId}/assign", content: null))
            .EnsureSuccessStatusCode();

    private Task<HttpResponseMessage> DecideAsync(Guid itemId, string outcome, string reasonCode, string note) =>
        HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/queue/{itemId}/decision", new { outcome, reasonCode, note });

    private Task<HttpResponseMessage> SecondReviewAsync(Guid itemId, bool confirm, string note) =>
        HttpClient.PostAsJsonAsync(
            $"{Trust}/moderation/queue/{itemId}/second-review", new { confirm, note });

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

    private sealed record CaseShape(Guid ItemId, Guid AuthorId, string? CurrentOutcome);
}
