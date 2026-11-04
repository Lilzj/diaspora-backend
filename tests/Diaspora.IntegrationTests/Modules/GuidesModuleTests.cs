using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Users;
using Diaspora.IntegrationTests.Common;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The Guides &amp; Knowledge Base Engine (B8), driven over real HTTP through the same host every
/// other module suite uses.
/// <para>
/// Three claims are under test at once. That the module composed into the platform through nothing
/// but contributor interfaces — its feature gates turn up in Identity's catalogue, its name in the
/// platform manifest, its sixty-one routes under one prefix — with no edit to the kernel, to
/// Identity, or to <c>Program.cs</c>. That a guide's whole life works end to end: draft, edit,
/// publish, version, compare, restore, archive, delete and come back. And the B8 rule that decides
/// who may do which part of that: <b>authoring is Limited, publishing is Verified</b>, because a
/// published guide is long-lived advice carrying the platform's name to people who will act on it.
/// </para>
/// <para>
/// The wire shapes below are re-declared by hand rather than referenced from the module. Binding
/// the tests to B8's own response records would make them pass whenever the module compiles, which
/// is the one thing a contract test must never do.
/// </para>
/// </summary>
public sealed class GuidesModuleTests : IdentityTestBase
{
    private const string Root = "/api/v1/guides";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public GuidesModuleTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Composition ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_guide_feature_gates_composed_into_the_platform_catalogue()
    {
        // Proof the module's IFeatureContributor reached the composed catalogue: gates B8 owns
        // turn up in Identity's /access/features response, which the Guides module never touched.
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        var names = features.Features.Select(feature => feature.Name).ToList();

        names.Should().Contain("guides.author", "authoring is a gate the Guides module owns");
        names.Should().Contain("guides.publish", "publishing is the module's progression gate");
        names.Should().Contain("guides.engage", "engagement is a gate the Guides module owns");
        names.Should().Contain("guides.suggest", "suggesting is a gate the Guides module owns");

        features.Features.Single(feature => feature.Name == "guides.author")
            .IsEnabled.Should().BeTrue("a Verified member is above the Limited floor for authoring");

        features.Features.Single(feature => feature.Name == "guides.publish")
            .IsEnabled.Should().BeTrue("a Verified member sits exactly on the publishing floor");
    }

    [Fact]
    public async Task The_publishing_gate_is_closed_for_a_limited_member()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        features.Features.Single(feature => feature.Name == "guides.author")
            .IsEnabled.Should().BeTrue("drafting opens at Limited — it is engagement, not a claim");

        features.Features.Single(feature => feature.Name == "guides.publish")
            .IsEnabled.Should().BeFalse("publishing is floored at Verified, and this member is Limited");
    }

    [Fact]
    public async Task The_platform_manifest_lists_the_guides_module()
    {
        var response = await HttpClient.GetAsync("/");

        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(
            "Guides", "the kernel discovered the module and published it in the manifest");
    }

    [Fact]
    public async Task The_capabilities_endpoint_reports_what_this_deployment_offers()
    {
        Api.ClearAuthentication();

        var capabilities = await ReadDataAsync<CapabilitiesDto>(
            await HttpClient.GetAsync($"{Root}/capabilities"));

        capabilities.Statuses.Should().Contain(
            ["Draft", "Published", "Archived"], "the client renders a status chip from this list");

        capabilities.BlockKinds.Should().Contain(
            ["Markdown", "Image", "Callout"], "the editor builds its block palette from this list");

        capabilities.VoteKinds.Should().Contain(
            ["Helpful", "NotHelpful"], "the helpful control sends one of these names back");

        capabilities.MaxSections.Should().BeGreaterThan(
            0, "a limit of zero would make the editor unusable");

        capabilities.MaxTags.Should().BeGreaterThan(
            0, "the chip editor caps itself from this number rather than a hard-coded copy");
    }

    // ── The lifecycle, end to end ───────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The suite's centrepiece.</b> One guide walked through its entire life in the order a
    /// real author would walk it, asserting at every stage both what the document says and who is
    /// allowed to read it. Split into a test per transition, this would pass while the transitions
    /// only worked in isolation — which is precisely the bug a knowledge base cannot afford.
    /// </summary>
    [Fact]
    public async Task A_guide_walks_its_whole_life_from_draft_through_versions_to_deletion_and_back()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        // ── Draft ───────────────────────────────────────────────────────────────────────────
        var draft = await CreateGuideAsync("Registering a residence permit in Lagos");

        draft.Status.Should().Be("Draft", "a new guide is always opened as a draft");
        draft.CurrentVersion.Should().Be(0, "nothing has been published, so no version exists yet");
        draft.PublishedAt.Should().BeNull("an unpublished guide has no publication instant");

        // The editor supplies section identifiers and keeps them stable across saves, which is
        // what lets the version diff below say "edited" instead of "replaced".
        var sectionId = Guid.NewGuid();

        // ── Metadata and structure ──────────────────────────────────────────────────────────
        var written = await UpdateGuideAsync(
            draft.GuideId,
            "Registering a residence permit in Lagos",
            SectionsPayload("Before you go", "Bring your passport and two photographs.", sectionId),
            summary: "What to bring, where to go, and how long it takes.");

        written.Sections.Should().ContainSingle(
            "the editor saved one section, and a whole-document save is wholesale");

        written.Sections[0].Blocks.Should().ContainSingle()
            .Which.Content.Should().Contain(
                "two photographs", "the block the author typed is the block that was stored");

        written.Revision.Should().BeGreaterThan(
            draft.Revision, "every save advances the optimistic-concurrency token");

        // ── First publication ───────────────────────────────────────────────────────────────
        var published = await PublishGuideAsync(draft.GuideId, "First edition.");

        published.Status.Should().Be("Published", "publishing is what makes a draft readable");
        published.CurrentVersion.Should().Be(1, "the first publication mints version 1");
        published.PublishedAt.Should().NotBeNull("a published guide records when it was published");

        var versionOne = await ReadDataAsync<VersionDto>(
            await HttpClient.GetAsync($"{Root}/{draft.GuideId}/versions/1"));

        versionOne.VersionNumber.Should().Be(1, "the history is addressed by version number");
        versionOne.RevisionNotes.Should().Be(
            "First edition.", "the author's own account of the change is what the history renders");
        versionOne.Sections.Should().ContainSingle()
            .Which.Title.Should().Be("Before you go", "a version is a snapshot of what readers saw");

        // ── Edit and republish ──────────────────────────────────────────────────────────────
        await UpdateGuideAsync(
            draft.GuideId,
            "Registering a residence permit in Lagos",
            SectionsPayload(
                "Before you go", "Bring your passport, two photographs and the fee.", sectionId));

        var republished = await PublishGuideAsync(draft.GuideId, "Added the fee.");

        republished.CurrentVersion.Should().Be(
            2, "republishing an edited draft mints the next version rather than overwriting one");

        // ── History ─────────────────────────────────────────────────────────────────────────
        var versions = await ReadDataAsync<IReadOnlyList<VersionSummaryDto>>(
            await HttpClient.GetAsync($"{Root}/{draft.GuideId}/versions"));

        versions.Should().HaveCount(2, "both publications are on the record");
        versions.Select(version => version.VersionNumber).Should().ContainInOrder(
            new[] { 2, 1 }, "the publication history reads newest first");

        var diff = await ReadDataAsync<VersionDiffDto>(
            await HttpClient.GetAsync(
                $"{Root}/{draft.GuideId}/versions/compare?fromVersion=1&toVersion=2"));

        diff.FromVersion.Should().Be(1, "the comparison answers about the versions it was asked about");
        diff.ToVersion.Should().Be(2, "the comparison answers about the versions it was asked about");
        diff.Sections.Should().ContainSingle()
            .Which.ContentChanged.Should().BeTrue(
                "the section is matched by its stable identifier, so this reads as edited not replaced");

        // ── Restoring an old version onto the draft ─────────────────────────────────────────
        var restoredVersion = await ReadDataAsync<GuideDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/{draft.GuideId}/versions/1/restore", new { }, JsonOptions));

        restoredVersion.Sections[0].Blocks[0].Content.Should().NotContain(
            "the fee", "restoring version 1 puts version 1's content back on the draft");

        restoredVersion.CurrentVersion.Should().Be(
            2, "a restore is an edit; only publishing mints a version, so the record is not rewritten");

        // ── Archive ─────────────────────────────────────────────────────────────────────────
        var archived = await ReadDataAsync<GuideDto>(
            await HttpClient.PostAsync($"{Root}/{draft.GuideId}/archive", content: null));

        archived.Status.Should().Be("Archived", "retiring stale advice is not a delete");
        archived.ArchivedAt.Should().NotBeNull("the archive records when the advice was retired");

        await AssertStrangerCannotReadAsync(
            draft.GuideId, "an archived guide is off the public shelves");

        // ── Restore from archive ────────────────────────────────────────────────────────────
        var unarchived = await ReadDataAsync<GuideDto>(
            await HttpClient.PostAsync($"{Root}/{draft.GuideId}/restore", content: null));

        unarchived.Status.Should().Be("Published", "restoring returns the guide exactly as it was");
        unarchived.ArchivedAt.Should().BeNull("the archive instant is cleared on restore");

        await AssertStrangerCanReadAsync(
            draft.GuideId, "a restored guide is readable again");

        // ── Soft delete, and back ───────────────────────────────────────────────────────────
        var deleted = await HttpClient.DeleteAsync($"{Root}/{draft.GuideId}");

        deleted.IsSuccessStatusCode.Should().BeTrue(
            "the author may remove their own guide: {0}",
            await deleted.Content.ReadAsStringAsync());

        var afterDelete = await HttpClient.GetAsync($"{Root}/{draft.GuideId}");

        afterDelete.StatusCode.Should().Be(
            HttpStatusCode.NotFound, "a deleted guide is gone even to the author who deleted it");

        var undeleted = await ReadDataAsync<GuideDto>(
            await HttpClient.PostAsync($"{Root}/{draft.GuideId}/undelete", content: null));

        undeleted.Status.Should().Be(
            "Published", "undeleting puts the guide back in whatever status it held");

        await AssertStrangerCanReadAsync(
            draft.GuideId, "the delete was soft, and undoing it restores public readability");
    }

    // ── Tier gating: authoring is Limited, publishing is Verified ────────────────────────────

    [Fact]
    public async Task A_limited_member_can_draft_and_edit_but_is_refused_publication()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        // Drafting costs the community nothing and is exactly what a new member should be doing.
        var draft = await CreateGuideAsync("Opening a bank account as a newcomer", "banking");

        var edited = await UpdateGuideAsync(
            draft.GuideId,
            "Opening a bank account as a newcomer",
            SectionsPayload("Documents", "A passport, a utility bill and a reference letter."));

        edited.Sections.Should().ContainSingle(
            "a Limited member's editor save is accepted in full");

        // Publishing long-lived advice under the platform's name is the progression gate.
        var refused = await HttpClient.PostAsJsonAsync(
            $"{Root}/{draft.GuideId}/publish", new { }, JsonOptions);

        refused.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "guides:publish is floored at Verified and this member holds Limited access");
    }

    [Fact]
    public async Task A_verified_member_can_publish_the_same_draft_a_limited_member_could_not()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var draft = await CreateGuideAsync("Enrolling a child in school", "education");

        await UpdateGuideAsync(
            draft.GuideId,
            "Enrolling a child in school",
            SectionsPayload("Term dates", "Applications open in January and close in March."));

        var published = await PublishGuideAsync(draft.GuideId);

        published.Status.Should().Be(
            "Published", "verification is the only thing that separated this member from the last");
    }

    // ── Visibility ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_stranger_asking_for_someone_elses_draft_is_told_it_does_not_exist()
    {
        var (_, authorAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(authorAuth.Tokens.AccessToken);

        var draft = await CreateGuideAsync("A half-written thing nobody should see");

        var (_, strangerAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(strangerAuth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync($"{Root}/{draft.GuideId}");

        // A 403 would be a confirmation that the draft exists, which is the leak the code is
        // written to avoid — visibility is applied inside the handler, not by the route table.
        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "an unauthorised reader must not be able to tell a private draft from a missing one");
    }

    [Fact]
    public async Task A_published_guide_is_readable_anonymously_by_id_and_by_slug()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var draft = await CreateGuideAsync("How to renew a driving licence", "transportation");

        await UpdateGuideAsync(
            draft.GuideId,
            "How to renew a driving licence",
            SectionsPayload("At the office", "Take the old licence and proof of address."));

        var published = await PublishGuideAsync(draft.GuideId);

        // A knowledge base that demands an account before it will answer has no way of
        // persuading anybody to make one.
        Api.ClearAuthentication();

        var byId = await ReadDataAsync<GuideDto>(
            await HttpClient.GetAsync($"{Root}/{draft.GuideId}"));

        byId.GuideId.Should().Be(
            draft.GuideId, "published knowledge is public and needs no authorization");

        var bySlug = await ReadDataAsync<GuideDto>(
            await HttpClient.GetAsync($"{Root}/slug/{published.Slug}"));

        bySlug.GuideId.Should().Be(
            draft.GuideId, "the slug is the handle a shared deep link carries");

        bySlug.Slug.Should().StartWith(
            "how-to-renew-a-driving-licence", "the slug is derived from the title when none is asked for");
    }

    // ── Discovery ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_finds_a_published_guide_by_the_words_in_its_title()
    {
        var guide = await PublishNewGuideAsync(
            "Applying for a Kaduna artisan permit", "employment", "NG");

        Api.ClearAuthentication();

        var page = await ReadDataAsync<CardPageDto>(
            await HttpClient.GetAsync($"{Root}?query=Kaduna"));

        page.Items.Should().Contain(
            card => card.GuideId == guide.GuideId,
            "free text runs over the title, and discovery is open to anybody");
    }

    [Fact]
    public async Task Search_filters_by_category_and_by_country()
    {
        var housing = await PublishNewGuideAsync(
            "Finding a flat share in Accra", "housing", "GH");

        Api.ClearAuthentication();

        var byCategory = await ReadDataAsync<CardPageDto>(
            await HttpClient.GetAsync($"{Root}?category=housing&country=GH"));

        byCategory.Items.Should().Contain(
            card => card.GuideId == housing.GuideId,
            "a guide filed under housing in Ghana matches both filters");

        var wrongCountry = await ReadDataAsync<CardPageDto>(
            await HttpClient.GetAsync($"{Root}?category=housing&country=KE"));

        wrongCountry.Items.Should().NotContain(
            card => card.GuideId == housing.GuideId,
            "a jurisdiction filter that let other countries through would be worse than none");
    }

    [Fact]
    public async Task A_draft_never_appears_in_discovery_even_to_its_own_author()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var draft = await CreateGuideAsync("Unfinished notes on Kwara tenancy law", "legal");

        await UpdateGuideAsync(
            draft.GuideId,
            "Unfinished notes on Kwara tenancy law",
            SectionsPayload("Notes", "Half a thought about deposits."));

        var page = await ReadDataAsync<CardPageDto>(
            await HttpClient.GetAsync($"{Root}?query=Kwara"));

        page.Items.Should().NotContain(
            card => card.GuideId == draft.GuideId,
            "publishing is an explicit act; nobody should discover their own half-written draft live");
    }

    [Fact]
    public async Task The_editorial_shelves_answer_and_latest_carries_what_was_just_published()
    {
        var guide = await PublishNewGuideAsync(
            "Registering with a clinic in Ibadan", "healthcare", "NG");

        Api.ClearAuthentication();

        var latest = await ReadDataAsync<CardPageDto>(
            await HttpClient.GetAsync($"{Root}/latest?countryCode=NG"));

        latest.Items.Should().Contain(
            card => card.GuideId == guide.GuideId,
            "latest is the one shelf with no editorial judgement in it");

        // Featured and trending are different editorial promises, and both must answer for a
        // client whose home screen renders all three shelves side by side.
        var featured = await HttpClient.GetAsync($"{Root}/featured");
        var trending = await HttpClient.GetAsync($"{Root}/trending?categoryKey=healthcare");

        featured.StatusCode.Should().Be(
            HttpStatusCode.OK, "the featured carousel answers even when staff have chosen nothing");

        trending.StatusCode.Should().Be(
            HttpStatusCode.OK, "the trending shelf answers even before anything has trended");

        var freshlyPublished = await ReadDataAsync<CardPageDto>(featured);

        freshlyPublished.Items.Should().NotContain(
            card => card.GuideId == guide.GuideId,
            "nothing in B8 can feature itself — placement is a staff act");
    }

    [Fact]
    public async Task Browsing_by_country_and_by_category_key_returns_the_published_guide()
    {
        var guide = await PublishNewGuideAsync(
            "Opening a Nairobi utility account", "utilities", "KE");

        Api.ClearAuthentication();

        var byCountry = await ReadDataAsync<CardPageDto>(
            await HttpClient.GetAsync($"{Root}/countries/KE"));

        byCountry.Items.Should().Contain(
            card => card.GuideId == guide.GuideId, "everything published about one country");

        var byCategory = await ReadDataAsync<CardPageDto>(
            await HttpClient.GetAsync($"{Root}/categories/utilities/guides"));

        byCategory.Items.Should().Contain(
            card => card.GuideId == guide.GuideId,
            "the category key is the address a browse URL carries");
    }

    [Fact]
    public async Task The_seeded_category_catalogue_is_served_to_anybody()
    {
        Api.ClearAuthentication();

        var categories = await ReadDataAsync<IReadOnlyList<CategoryDto>>(
            await HttpClient.GetAsync($"{Root}/categories"));

        categories.Should().NotBeEmpty(
            "a knowledge base with no categories cannot have a guide drafted against it");

        categories.Select(category => category.Key).Should().Contain(
            ["immigration", "housing", "healthcare", "education"],
            "these are the catalogue keys the module's own contributor seeds");

        categories.Should().OnlyContain(
            category => category.IsActive,
            "retired categories stay out of the picker unless they are asked for");

        var immigration = categories.Single(category => category.Key == "immigration");

        immigration.Name.Should().NotBeNullOrWhiteSpace(
            "the picker renders the name, not the key");
    }

    [Fact]
    public async Task The_tag_cloud_is_built_from_what_authors_actually_tagged()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var draft = await CreateGuideAsync("Sending money home cheaply", "banking", "NG");

        await UpdateGuideAsync(
            draft.GuideId,
            "Sending money home cheaply",
            SectionsPayload("Fees", "Compare the spread, not the advertised fee."));

        var tagged = await ReadDataAsync<GuideDto>(
            await HttpClient.PutAsJsonAsync(
                $"{Root}/{draft.GuideId}/tags",
                new { tags = new[] { "Remittances", "remittances", "  Fees  " } },
                JsonOptions));

        tagged.Tags.Should().BeEquivalentTo(
            ["remittances", "fees"],
            "the aggregate normalizes each entry to a slug and deduplicates what collides");

        await PublishGuideAsync(draft.GuideId);

        Api.ClearAuthentication();

        var tags = await ReadDataAsync<IReadOnlyList<TagDto>>(
            await HttpClient.GetAsync($"{Root}/tags?countryCode=NG"));

        tags.Should().Contain(
            tag => tag.Tag == "remittances" && tag.Count > 0,
            "the cloud is counted from real guides rather than a curated list");
    }

    // ── Engagement ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Saving_a_guide_and_unsaving_it_are_both_idempotent()
    {
        var guide = await PublishNewGuideAsync("Choosing a Lagos school run", "education", "NG");

        var (_, readerAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(readerAuth.Tokens.AccessToken);

        // A PUT and a DELETE on one resource rather than two verbs on a collection, so a client
        // retrying after a timeout gets the same answer as the first attempt.
        var firstSave = await HttpClient.PutAsync($"{Root}/{guide.GuideId}/bookmark", content: null);
        var secondSave = await HttpClient.PutAsync($"{Root}/{guide.GuideId}/bookmark", content: null);

        firstSave.IsSuccessStatusCode.Should().BeTrue("saving a guide is an ordinary member act");
        secondSave.IsSuccessStatusCode.Should().BeTrue("a double-tapped bookmark cannot produce two");

        var saved = await ReadDataAsync<CardPageDto>(
            await HttpClient.GetAsync($"{Root}/bookmarked"));

        saved.Items.Should().ContainSingle(card => card.GuideId == guide.GuideId,
            "the saved list is the member's own, and holds one entry per guide");

        var firstRemoval = await HttpClient.DeleteAsync($"{Root}/{guide.GuideId}/bookmark");

        firstRemoval.IsSuccessStatusCode.Should().BeTrue("a member may unsave what they saved");

        var secondRemoval = await HttpClient.DeleteAsync($"{Root}/{guide.GuideId}/bookmark");

        secondRemoval.IsSuccessStatusCode.Should().BeTrue(
            "removing a bookmark that is already gone is the state the caller asked for, so a " +
            "client retrying a DELETE after a dropped response must not be told it failed");

        var afterRemoval = await ReadDataAsync<CardPageDto>(
            await HttpClient.GetAsync($"{Root}/bookmarked"));

        afterRemoval.Items.Should().NotContain(
            card => card.GuideId == guide.GuideId, "the guide left the saved list");
    }

    /// <summary>
    /// <b>The guide read tells its reader what they did to it</b> (UAT, 3 Nov 2026). Saving a
    /// guide left the button reading "Save guide", because nothing on the read said the reader
    /// had saved it; the helpful vote was a flag that reset when the screen closed.
    /// </summary>
    [Fact]
    public async Task The_guide_read_tells_its_reader_whether_they_saved_it_and_how_they_voted()
    {
        var guide = await PublishNewGuideAsync("Registering a child at a Lagos school", "education", "NG");

        var (_, readerAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(readerAuth.Tokens.AccessToken);

        var before = await ViewerOfAsync($"{Root}/{guide.GuideId}");
        before.Should().NotBeNull("a signed-in reader has a state, even an empty one");
        before!.Value.GetProperty("isSaved").GetBoolean().Should().BeFalse();
        VoteOf(before.Value).Should().BeNull();

        (await HttpClient.PutAsync($"{Root}/{guide.GuideId}/bookmark", content: null))
            .IsSuccessStatusCode.Should().BeTrue();
        (await HttpClient.PostAsJsonAsync(
                $"{Root}/{guide.GuideId}/vote", new { kind = "Helpful" }, JsonOptions))
            .IsSuccessStatusCode.Should().BeTrue();

        var after = (await ViewerOfAsync($"{Root}/{guide.GuideId}"))!.Value;
        after.GetProperty("isSaved").GetBoolean().Should().BeTrue("the reader saved it");
        VoteOf(after).Should().Be("Helpful", "the reader marked it helpful");

        var bySlug = (await ViewerOfAsync($"{Root}/slug/{guide.Slug}"))!.Value;
        bySlug.GetProperty("isSaved").GetBoolean().Should().BeTrue("a deep link reads the same guide");

        (await HttpClient.DeleteAsync($"{Root}/{guide.GuideId}/bookmark"))
            .IsSuccessStatusCode.Should().BeTrue();

        (await ViewerOfAsync($"{Root}/{guide.GuideId}"))!.Value
            .GetProperty("isSaved").GetBoolean().Should().BeFalse("the reader removed the save");

        Api.ClearAuthentication();

        (await ViewerOfAsync($"{Root}/{guide.GuideId}"))
            .Should().BeNull("a signed-out reader has nothing of their own on the guide");
    }

    /// <summary>The guide read's <c>viewer</c>, or null when it is null or absent.</summary>
    private async Task<JsonElement?> ViewerOfAsync(string url)
    {
        var response = await HttpClient.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        var data = document.RootElement.GetProperty("data");

        return data.TryGetProperty("viewer", out var viewer) && viewer.ValueKind == JsonValueKind.Object
            ? viewer.Clone()
            : null;
    }

    private static string? VoteOf(JsonElement viewer) =>
        viewer.TryGetProperty("vote", out var vote) && vote.ValueKind == JsonValueKind.String
            ? vote.GetString()
            : null;

    [Fact]
    public async Task A_helpful_vote_moves_the_count_and_recasting_changes_it_rather_than_adding_one()
    {
        var guide = await PublishNewGuideAsync("Reading a Ghanaian payslip", "employment", "GH");

        var (_, readerAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(readerAuth.Tokens.AccessToken);

        var helpful = await ReadDataAsync<EngagementDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/{guide.GuideId}/vote", new { kind = "Helpful" }, JsonOptions));

        helpful.HelpfulCount.Should().Be(1, "the reader's verdict is counted immediately");
        helpful.NotHelpfulCount.Should().Be(0, "only one kind of vote was cast");

        var changed = await ReadDataAsync<EngagementDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/{guide.GuideId}/vote", new { kind = "NotHelpful" }, JsonOptions));

        // One vote per member per guide: casting the other kind moves the vote across rather
        // than letting one reader count twice.
        changed.HelpfulCount.Should().Be(0, "the member's earlier helpful vote was withdrawn");
        changed.NotHelpfulCount.Should().Be(1, "and replaced by the vote they actually hold");
    }

    [Fact]
    public async Task A_view_is_recorded_for_a_reader_who_has_no_account_at_all()
    {
        var guide = await PublishNewGuideAsync("Catching a Kampala boda safely", "safety", "UG");

        // Anonymous readers are most of the traffic on a knowledge base, and a counter that only
        // counted signed-in members would report the wrong number confidently.
        Api.ClearAuthentication();

        // A completed read on a guide with no analytics row yet, which is the case that used to
        // insert two rows and violate the one-row-per-guide index: the recorder is asked twice in
        // one request — once for the view, once for the completion that arrived with it.
        var recorded = await HttpClient.PostAsJsonAsync(
            $"{Root}/{guide.GuideId}/view",
            new { isUniqueView = true, completed = true, readSeconds = 90 },
            JsonOptions);

        recorded.IsSuccessStatusCode.Should().BeTrue(
            "recording a view carries no authorization: {0}",
            await recorded.Content.ReadAsStringAsync());

        var reread = await ReadDataAsync<GuideDto>(
            await HttpClient.GetAsync($"{Root}/{guide.GuideId}"));

        reread.Engagement.Views.Should().BeGreaterThan(
            0, "the view the anonymous reader reported reached the guide's rollup");
    }

    [Fact]
    public async Task A_reader_can_suggest_an_improvement_and_the_suggestion_is_listed_publicly()
    {
        var guide = await PublishNewGuideAsync("Renewing a Kenyan passport", "immigration", "KE");

        var (_, readerAuth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(readerAuth.Tokens.AccessToken);

        var submitted = await HttpClient.PostAsJsonAsync(
            $"{Root}/{guide.GuideId}/suggestions",
            new
            {
                proposedContent = "The fee rose to 7,500 shillings in June.",
                rationale = "I paid it last week."
            },
            JsonOptions);

        submitted.StatusCode.Should().Be(
            HttpStatusCode.Created,
            "a suggestion is the platform's lightweight merge request: {0}",
            await submitted.Content.ReadAsStringAsync());

        var suggestion = await ReadDataAsync<SuggestionDto>(submitted);

        suggestion.GuideId.Should().Be(guide.GuideId, "the suggestion targets the guide it was sent to");
        suggestion.Status.Should().Be("Open", "nobody has decided on it yet");

        // Listing is public: seeing that somebody already reported the fee as out of date saves
        // the next reader the trouble and the author the tenth identical correction.
        Api.ClearAuthentication();

        var listed = await ReadDataAsync<SuggestionPageDto>(
            await HttpClient.GetAsync($"{Root}/{guide.GuideId}/suggestions"));

        listed.Items.Should().ContainSingle(
            item => item.SuggestionId == suggestion.SuggestionId,
            "the improvement suggestions on one guide are readable by anybody");
    }

    [Fact]
    public async Task A_member_may_not_suggest_an_improvement_to_their_own_guide()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var guide = await PublishAsAuthorAsync("Notes I wrote myself", "culture", "NG");

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/{guide.GuideId}/suggestions",
            new { proposedContent = "I could just edit this." },
            JsonOptions);

        // The author already has the editor; a self-suggestion is a queue entry nobody needs.
        response.IsSuccessStatusCode.Should().BeFalse(
            "suggesting to yourself is refused rather than silently accepted");

        (await response.Content.ReadAsStringAsync()).Should().Contain(
            "guides.suggestion", "the refusal names a Guides error the client can branch on");
    }

    // ── Moderation ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_moderator_can_take_a_guide_off_the_public_shelves_and_put_it_back()
    {
        var guide = await PublishNewGuideAsync(
            "Advice that turned out to be wrong", "legal", "NG");

        var moderator = await AuthenticateAsStaffAsync("Moderator");

        var hidden = await ReadDataAsync<GuideDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/{guide.GuideId}/hide",
                new { reason = "The stated filing deadline is wrong and readers are missing it." },
                JsonOptions));

        hidden.ModerationState.Should().Be(
            "Hidden", "hiding is reversible removal, not a delete");

        await AssertStrangerCannotReadAsync(
            guide.GuideId, "a hidden guide is off every public surface");

        // A moderator can still see what they are deciding about — the one read in this module
        // that deliberately sees what nobody else does.
        Api.Authenticate(moderator);

        var queue = await HttpClient.GetAsync($"{Root}/moderation/queue?moderationState=Hidden");

        queue.StatusCode.Should().Be(
            HttpStatusCode.OK, "the moderation queue is readable by the staff who work it");

        var restored = await ReadDataAsync<GuideDto>(
            await HttpClient.PostAsync($"{Root}/{guide.GuideId}/unhide", content: null));

        restored.ModerationState.Should().Be(
            "Clear", "lifting a hide restores exactly what was there");

        await AssertStrangerCanReadAsync(
            guide.GuideId, "the guide is back on the public shelves");
    }

    [Fact]
    public async Task An_ordinary_verified_member_cannot_hide_anybody_elses_guide()
    {
        var guide = await PublishNewGuideAsync("Perfectly good advice", "banking", "NG");

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/{guide.GuideId}/hide", new { reason = "I disagree with it." }, JsonOptions);

        // Moderation is role-scoped, not tier-scoped: a tier measures progression, a role grants
        // privilege, and no amount of verification buys this one.
        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden, "guides:moderate is held by staff roles alone");
    }

    // ── Outbox: B8 publishes through B1.1 rather than in process ─────────────────────────────

    /// <summary>
    /// The proof that the Guides module announces itself through the transactional outbox rather
    /// than an in-process call: publishing leaves a committed, durable row naming a contract in
    /// <c>Diaspora.Contracts</c>, and draining delivers it to real consumers without a dead letter.
    /// </summary>
    [Fact]
    public async Task Publishing_leaves_a_durable_guide_published_message_that_the_outbox_delivers()
    {
        // Start from a quiet queue so what is pending afterwards is this test's own doing.
        await Factory.DrainOutboxAsync();

        await PublishNewGuideAsync("Getting a Tanzanian TIN", "taxes", "TZ");

        var pending = await Factory.PendingOutboxAsync();

        var published = pending
            .Where(message => message.EventType.Contains("GuidePublished", StringComparison.Ordinal))
            .ToList();

        published.Should().NotBeEmpty(
            "publishing a guide must leave a committed promise to announce it");

        published.Should().OnlyContain(
            message => message.EventType.StartsWith("Diaspora.Contracts.", StringComparison.Ordinal),
            "the stored identity is a contract name, never a CLR assembly-qualified type");

        published.Should().OnlyContain(
            message => message.Payload.Length > 0,
            "a message with no payload tells a consumer nothing");

        var delivered = await Factory.DrainOutboxAsync();

        delivered.Should().BeGreaterThan(0, "the queued messages were delivered on demand");

        (await Factory.PendingOutboxAsync())
            .Where(message => message.EventType.Contains("GuidePublished", StringComparison.Ordinal))
            .Should().BeEmpty("every guide-published message reached a consumer that accepted it");

        (await Factory.DeadLettersAsync())
            .Where(message => message.EventType.Contains("Guide", StringComparison.Ordinal))
            .Should().BeEmpty("no consumer rejected a message the Guides module produced");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a fresh verified member, writes a guide with one real section and publishes it.
    /// The caller is left authenticated as somebody else's reader would not be — tests that need
    /// a stranger clear the authentication themselves.
    /// </summary>
    private async Task<GuideDto> PublishNewGuideAsync(
        string title, string categoryKey, string countryCode)
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        return await PublishAsAuthorAsync(title, categoryKey, countryCode);
    }

    /// <summary>Writes and publishes a guide as whoever is currently authenticated.</summary>
    private async Task<GuideDto> PublishAsAuthorAsync(
        string title, string categoryKey, string countryCode)
    {
        var draft = await CreateGuideAsync(title, categoryKey, countryCode);

        await UpdateGuideAsync(
            draft.GuideId,
            title,
            SectionsPayload("What to do", "Take the form, the fee and one hour of patience."),
            countryCode);

        return await PublishGuideAsync(draft.GuideId, "First edition.");
    }

    /// <summary>
    /// Registers a member, grants them a platform role, and signs them in again so the token
    /// carries it.
    /// <para>
    /// <see cref="IdentityTestBase"/> offers no staff helper — the only credential it can mint is
    /// the Trust Engine's service token, which grants one permission and no role. The role is
    /// therefore assigned through the aggregate's own <c>AssignRole</c> method rather than by
    /// writing a column, and the re-login is what puts the claim in the JWT the module reads.
    /// </para>
    /// </summary>
    private async Task<string> AuthenticateAsStaffAsync(string roleName)
    {
        var (phone, auth) = await RegisterVerifiedMemberAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

            var userId = new UserId(auth.User.Id);

            var user = await dbContext.Set<User>()
                .FirstOrDefaultAsync(candidate => candidate.Id == userId);

            user.Should().NotBeNull("the member was registered a moment ago");

            user!.AssignRole(UserRole.All.Single(role => role.Name == roleName));

            await dbContext.SaveChangesAsync();
        }

        var login = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(
            await Api.LoginAsync(new LoginRequest { Email = EmailFor(phone), Password = ValidPassword }));

        Api.Authenticate(login.Tokens.AccessToken);

        return login.Tokens.AccessToken;
    }

    /// <summary>
    /// Reads the guide as somebody with no credential at all, restoring the caller's
    /// authentication afterwards so the calling test can carry on as itself.
    /// </summary>
    private async Task<HttpStatusCode> ReadAnonymouslyAsync(Guid guideId)
    {
        var authorization = HttpClient.DefaultRequestHeaders.Authorization;

        try
        {
            Api.ClearAuthentication();

            var response = await HttpClient.GetAsync($"{Root}/{guideId}");

            return response.StatusCode;
        }
        finally
        {
            HttpClient.DefaultRequestHeaders.Authorization = authorization;
        }
    }

    private async Task AssertStrangerCanReadAsync(Guid guideId, string because) =>
        (await ReadAnonymouslyAsync(guideId)).Should().Be(HttpStatusCode.OK, because);

    private async Task AssertStrangerCannotReadAsync(Guid guideId, string because) =>
        (await ReadAnonymouslyAsync(guideId)).Should().Be(HttpStatusCode.NotFound, because);


    private async Task<GuideDto> CreateGuideAsync(
        string title,
        string categoryKey = "immigration",
        string countryCode = "NG",
        string language = "en",
        string? summary = null)
    {
        var response = await HttpClient.PostAsJsonAsync(
            Root,
            new
            {
                title,
                categoryKey,
                language,
                countryCode,
                summary
            },
            JsonOptions);

        response.StatusCode.Should().Be(
            HttpStatusCode.Created,
            "creating a draft is a Limited-tier capability: {0}",
            await response.Content.ReadAsStringAsync());

        return await ReadDataAsync<GuideDto>(response);
    }

    /// <summary>One section holding one markdown block — the least a guide may publish with.</summary>
    private static SectionInput[] SectionsPayload(
        string sectionTitle, string body, Guid? sectionId = null) =>
        [
            new SectionInput(
                sectionId ?? Guid.NewGuid(),
                sectionTitle,
                0,
                [new BlockInput(Guid.NewGuid(), "Markdown", 0, body)])
        ];

    private async Task<GuideDto> UpdateGuideAsync(
        Guid guideId,
        string title,
        SectionInput[]? sections = null,
        string countryCode = "NG",
        string? summary = null)
    {
        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/{guideId}",
            new
            {
                title,
                summary,
                countryCode,
                sections
            },
            JsonOptions);

        return await ReadDataAsync<GuideDto>(response);
    }

    private async Task<GuideDto> PublishGuideAsync(Guid guideId, string? revisionNotes = null)
    {
        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/{guideId}/publish",
            new { revisionNotes },
            JsonOptions);

        return await ReadDataAsync<GuideDto>(response);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Expected success but got {(int)response.StatusCode}. Body: {body}");
        }

        var envelope = JsonSerializer.Deserialize<Envelope<T>>(body, JsonOptions)
            ?? throw new InvalidOperationException($"Could not deserialize: {body}");

        return envelope.Data;
    }

    // ── Wire shapes, deliberately re-declared rather than referenced ─────────────────────────

    private sealed record Envelope<T>(T Data);

    private sealed record SectionInput(
        Guid SectionId, string Title, int Order, IReadOnlyList<BlockInput> Blocks);

    private sealed record BlockInput(Guid BlockId, string Kind, int Order, string? Content);

    private sealed record GuideDto(
        Guid GuideId,
        string Slug,
        string Title,
        string? Summary,
        string CategoryKey,
        string Language,
        string Status,
        string Visibility,
        string ModerationState,
        bool IsFeatured,
        string CountryCode,
        int CurrentVersion,
        int Revision,
        Guid AuthorId,
        DateTimeOffset? PublishedAt,
        DateTimeOffset? ArchivedAt,
        IReadOnlyList<SectionDto> Sections,
        IReadOnlyList<string> Tags,
        EngagementDto Engagement);

    private sealed record SectionDto(
        Guid SectionId, string Title, int Order, IReadOnlyList<BlockDto> Blocks);

    private sealed record BlockDto(
        Guid BlockId, string Kind, int Order, string? Content, Guid? MediaId, string? Metadata);

    private sealed record EngagementDto(
        long Views,
        long UniqueViews,
        int HelpfulCount,
        int NotHelpfulCount,
        int BookmarkCount,
        int ShareCount,
        int CommentCount);

    private sealed record CardPageDto(
        IReadOnlyList<CardDto> Items, string? NextCursor, bool HasMore, int? TotalCount);

    private sealed record CardDto(
        Guid GuideId,
        string Slug,
        string Title,
        string CategoryKey,
        string CountryCode,
        string Language,
        string Status,
        bool IsVerified,
        bool IsFeatured,
        Guid AuthorId,
        long Views,
        int HelpfulCount,
        IReadOnlyList<string> Tags);

    private sealed record CategoryDto(
        Guid CategoryId,
        string Key,
        string Name,
        string? Description,
        string? Icon,
        int Order,
        bool IsActive,
        string? ParentKey);

    private sealed record TagDto(string Tag, int Count);

    private sealed record SuggestionDto(
        Guid SuggestionId,
        Guid GuideId,
        Guid AuthorId,
        Guid? TargetSectionId,
        string ProposedContent,
        string? Rationale,
        string Status,
        DateTimeOffset CreatedAt);

    private sealed record SuggestionPageDto(
        IReadOnlyList<SuggestionDto> Items, string? NextCursor, bool HasMore);

    private sealed record VersionDto(
        Guid VersionId,
        Guid GuideId,
        int VersionNumber,
        string Title,
        string? RevisionNotes,
        Guid EditorId,
        DateTimeOffset PublishedAt,
        IReadOnlyList<SectionDto> Sections);

    private sealed record VersionSummaryDto(
        Guid VersionId,
        Guid GuideId,
        int VersionNumber,
        string Title,
        string? RevisionNotes,
        DateTimeOffset PublishedAt);

    private sealed record VersionDiffDto(
        Guid GuideId, int FromVersion, int ToVersion, IReadOnlyList<SectionDiffDto> Sections);

    private sealed record SectionDiffDto(
        Guid SectionId, string Change, string? FromTitle, string? ToTitle, bool ContentChanged);

    private sealed record CapabilitiesDto(
        IReadOnlyList<string> Statuses,
        IReadOnlyList<string> Visibilities,
        IReadOnlyList<string> BlockKinds,
        IReadOnlyList<string> Difficulties,
        IReadOnlyList<string> SortOrders,
        IReadOnlyList<string> VoteKinds,
        int MaxSections,
        int MaxTags,
        int MaxPageSize);
}
