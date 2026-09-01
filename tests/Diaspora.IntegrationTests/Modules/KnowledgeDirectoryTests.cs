using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Jobs.V1.Events;
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
/// The Country Knowledge Directory (B8.1), driven over real HTTP through the same host every other
/// module suite uses.
/// <para>
/// Three claims are under test at once. That the directory composed into the platform through
/// nothing but contributor interfaces — a second permission contributor, a second endpoint
/// contributor, a second telemetry source, seventeen tables in B8's own schema — with no edit to the
/// kernel, to Identity, or to <c>Program.cs</c>. That a country's knowledge works end to end: open a
/// directory, arrange it, publish it, feature a guide, read the page anonymously, search it, walk an
/// onboarding path. And the rule that decides who may do which part: <b>reading is anonymous and
/// curation is role-scoped</b>, because a knowledge base that demands an account before it answers
/// "how do I register a residence permit" has no way of persuading anybody to make one, and the page
/// every newcomer sees is not something a member should reach by verifying a phone number.
/// </para>
/// <para>
/// The wire shapes below are re-declared by hand rather than referenced from the module. Binding the
/// tests to B8.1's own response records would make them pass whenever the module compiles, which is
/// the one thing a contract test must never do.
/// </para>
/// </summary>
public sealed class KnowledgeDirectoryTests : IdentityTestBase
{
    private const string Root = "/api/v1/knowledge";
    private const string GuidesRoot = "/api/v1/guides";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public KnowledgeDirectoryTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Composition ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_navigation_catalogue_was_seeded_by_the_modules_own_hosted_service()
    {
        Api.ClearAuthentication();

        var categories = await ReadDataAsync<IReadOnlyList<CategoryDto>>(
            await HttpClient.GetAsync($"{Root}/categories"));

        categories.Should().NotBeEmpty(
            "a directory with no navigation is an empty menu on every country page");

        categories.Select(category => category.Key).Should().Contain(
            ["getting-started", "immigration", "housing", "healthcare", "cost-of-living"],
            "the shipped catalogue is ordered by the sequence of arriving somewhere");

        categories.Should().OnlyContain(
            category => category.IsActive, "archived rows stay out unless a curator asks");

        // The source expansion is the seam between curation and content, and it must survive the
        // round trip: a row with no declared sources answers with the filing key it shares.
        categories.Single(category => category.Key == "housing")
            .SourceCategoryKeys.Should().Contain("housing");

        categories.Single(category => category.Key == "student-life")
            .SourceCategoryKeys.Should().Contain(
                ["student-life", "education"],
                "a navigation row legitimately draws from more than one filing category");
    }

    [Fact]
    public async Task The_curation_gates_composed_into_the_platform_catalogue()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        var gate = features.Features.SingleOrDefault(
            feature => feature.Name == "guides.knowledge.curate");

        gate.Should().NotBeNull(
            "the module's second IFeatureContributor reached Identity's composed catalogue");

        gate!.IsEnabled.Should().BeFalse(
            "curation is role-scoped: no amount of verifying a phone number buys editorial " +
            "authority over the page every newcomer to a country sees");
    }

    [Fact]
    public async Task The_capabilities_endpoint_reports_what_this_deployment_registered()
    {
        Api.ClearAuthentication();

        var capabilities = await ReadDataAsync<CapabilitiesDto>(
            await HttpClient.GetAsync($"{Root}/capabilities"));

        capabilities.Sections.Should().Contain(
            ["featured", "most-helpful", "recently-updated", "official", "community"],
            "a client renders whichever shelves this deployment registered, not a hard-coded list");

        capabilities.Sorts.Should().Contain("relevance");
        capabilities.Difficulties.Should().Contain(["beginner", "advanced"]);

        capabilities.SearchEngine.Should().NotBeNullOrWhiteSpace(
            "the answer to why search behaves differently in production is usually the engine");

        capabilities.AnalyticsSinks.Should().Contain(
            ["country-counters", "search-terms"],
            "the shipped sinks are what make the dashboard work with nothing configured");

        capabilities.ContextModules.Should().Contain(
            "Guides", "the projected counter contributor ships enabled");

        capabilities.CategoryCount.Should().BeGreaterThan(0);
        capabilities.MaxPathSteps.Should().BeGreaterThan(0);
    }

    // ── A country, end to end ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The suite's centrepiece.</b> One country walked from nothing to a readable page in the
    /// order a real curator would walk it, asserting at every stage both what the page says and who
    /// is allowed to see it. Split into a test per transition, this would pass while the transitions
    /// only worked in isolation — which for the platform's most-read screen is precisely the bug
    /// nobody would notice until launch day.
    /// </summary>
    [Fact]
    public async Task A_country_goes_from_nothing_to_a_readable_knowledge_page()
    {
        const string Country = "MU";

        var guideId = await PublishGuideAsync("Applying for a Mauritian occupation permit", "immigration", Country);

        await AuthenticateAsStaffAsync("Admin");

        // ── Open the country ────────────────────────────────────────────────────────────────
        var created = await ReadDataAsync<DirectoryDetailDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/admin/countries/{Country}",
                new
                {
                    displayName = "Mauritius",
                    summary = "Everything you need for your first months on the island.",
                    primaryLanguage = "en",
                    languages = new[] { "en", "fr" }
                },
                JsonOptions));

        created.CountryCode.Should().Be(Country);
        created.IsPublished.Should().BeFalse("publishing is a separate, deliberate act");

        created.Entries.Should().NotBeEmpty(
            "a new country is arranged from a template — a curator should not start from a blank page");

        created.Entries[0].KnowledgeCategoryKey.Should().Be(
            "getting-started", "arriving somewhere begins with getting oriented");

        created.Entries.Should().Contain(
            entry => entry.IsHidden,
            "rows with no guides yet are arranged in the right place and hidden, so the curator " +
            "un-hides one the day somebody writes the first guide");

        // ── Unpublished is invisible to a reader ────────────────────────────────────────────
        (await ReadAnonymouslyAsync($"{Root}/countries/{Country}")).Should().Be(
            HttpStatusCode.NotFound,
            "that a country is being prepared is not something a reader should learn from a 403");

        // ── Arrange, then publish ───────────────────────────────────────────────────────────
        await AuthenticateAsStaffAsync("Admin");

        var arranged = await ReadDataAsync<DirectoryDetailDto>(
            await HttpClient.PutAsJsonAsync(
                $"{Root}/admin/countries/{Country}/arrangement",
                new
                {
                    entries = new object[]
                    {
                        new { knowledgeCategoryKey = "immigration", labelOverride = "Residency", isHidden = false },
                        new { knowledgeCategoryKey = "housing", isHidden = false },
                        new { knowledgeCategoryKey = "retirement", isHidden = true }
                    },
                    revision = created.Revision
                },
                JsonOptions));

        arranged.Entries.Select(entry => entry.Position).Should().BeEquivalentTo(
            [0, 1, 2], "position comes from the list order, never from a number the client sent");

        arranged.Revision.Should().BeGreaterThan(created.Revision);

        (await HttpClient.PostAsync($"{Root}/admin/countries/{Country}/publish", null))
            .IsSuccessStatusCode.Should().BeTrue();

        // ── The reader's page ───────────────────────────────────────────────────────────────
        Api.ClearAuthentication();

        var page = await ReadDataAsync<CountryDirectoryDto>(
            await HttpClient.GetAsync($"{Root}/countries/{Country}"));

        page.DisplayName.Should().Be("Mauritius");
        page.IsPublished.Should().BeTrue();
        page.Languages.Should().BeEquivalentTo(["en", "fr"]);

        page.Categories.Should().HaveCount(
            2, "the hidden row is arranged and not shown");

        page.Categories[0].Label.Should().Be(
            "Residency",
            "the country's own wording wins over the catalogue's — the term the local paperwork " +
            "uses is the term readers will recognise");

        page.Categories[0].GuideCount.Should().Be(
            1, "a reader deciding where to start is guided far more by a count than by a label");

        page.Categories.Single(entry => entry.KnowledgeCategoryKey == "housing")
            .GuideCount.Should().Be(0, "an empty row tells them honestly this is not where the answers are");

        page.Statistics.TotalGuides.Should().Be(1);
        page.Statistics.CategoryCount.Should().Be(2);

        // ── Curate a shelf ──────────────────────────────────────────────────────────────────
        await AuthenticateAsStaffAsync("Moderator");

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/admin/countries/{Country}/featured",
                new { guideId, note = "Read this before you book a flight." },
                JsonOptions))
            .IsSuccessStatusCode.Should().BeTrue(
                "curation is held by moderators as well as administrators — deciding what goes " +
                "first is the same judgement as deciding what stays up");

        Api.ClearAuthentication();

        var curated = await ReadDataAsync<CountryDirectoryDto>(
            await HttpClient.GetAsync($"{Root}/countries/{Country}"));

        curated.FeaturedGuides.Should().ContainSingle()
            .Which.GuideId.Should().Be(
                guideId, "featuring is country-scoped and never touches the guide itself");

        // ── The category page ───────────────────────────────────────────────────────────────
        var category = await ReadDataAsync<CategoryViewDto>(
            await HttpClient.GetAsync($"{Root}/countries/{Country}/categories/immigration"));

        category.CountryName.Should().Be("Mauritius");
        category.Label.Should().Be("Residency");
        category.GuideCount.Should().Be(1);

        category.Sections.Should().NotBeEmpty(
            "whichever section providers this deployment registered filled the page");

        category.Sections.Should().OnlyContain(
            section => section.Guides.Count > 0,
            "a heading over an empty list tells a reader nothing a missing heading does not");

        category.ProviderCategoryKeys.Should().Contain(
            "immigration-consultant",
            "the directory says which kind of professional the subject calls for; B7 says who they are");

        // ── The dashboard ───────────────────────────────────────────────────────────────────
        var dashboard = await ReadDataAsync<DashboardDto>(
            await HttpClient.GetAsync($"{Root}/countries/{Country}/dashboard"));

        dashboard.DisplayName.Should().Be("Mauritius");
        dashboard.Featured.Should().ContainSingle();
        dashboard.CategoryBreakdown.Should().NotBeEmpty();
        dashboard.Statistics.TotalGuides.Should().Be(1);

        dashboard.Context.Should().Contain(
            block => block.Kind == "guides" && block.Count == 1,
            "the guide band is B8's own count, asked live rather than projected");
    }

    // ── Reading is anonymous, curation is role-scoped ───────────────────────────────────────

    [Fact]
    public async Task Every_read_answers_without_a_credential()
    {
        const string Country = "GH";

        await PublishGuideAsync("Opening a Ghanaian bank account", "banking", Country);
        await OpenAndPublishCountryAsync(Country, "Ghana", "banking");

        Api.ClearAuthentication();

        foreach (var route in new[]
        {
            $"{Root}/countries",
            $"{Root}/countries/{Country}",
            $"{Root}/countries/{Country}/dashboard",
            $"{Root}/countries/{Country}/statistics",
            $"{Root}/countries/{Country}/categories/banking",
            $"{Root}/countries/{Country}/paths",
            $"{Root}/categories",
            $"{Root}/capabilities",
            $"{Root}/search?countryCode={Country}",
            $"{Root}/popular?countryCode={Country}",
            $"{Root}/official?countryCode={Country}"
        })
        {
            var response = await HttpClient.GetAsync(route);

            response.StatusCode.Should().Be(
                HttpStatusCode.OK,
                "a knowledge base that demands an account before it answers has no way of " +
                "persuading anybody to make one: {0}", route);
        }
    }

    [Fact]
    public async Task A_verified_member_cannot_curate_a_country()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/admin/countries/KE",
            new { displayName = "Kenya" },
            JsonOptions);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "a tier measures progression and a role grants privilege — B2.0.1, applied to the " +
            "page every newcomer to a country sees");
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_curate_a_country()
    {
        Api.ClearAuthentication();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/admin/countries/KE",
            new { displayName = "Kenya" },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Curation guards ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The guard that matters most on this surface.</b> A curator who mistypes a country code
    /// would otherwise silently recommend Canadian tax guidance to somebody moving to Mauritius,
    /// which is exactly the failure a knowledge base cannot afford.
    /// </summary>
    [Fact]
    public async Task A_guide_about_a_different_country_cannot_be_featured()
    {
        const string Country = "RW";

        var foreign = await PublishGuideAsync("A guide about somewhere else", "immigration", "TZ");

        await OpenAndPublishCountryAsync(Country, "Rwanda", "immigration");

        await AuthenticateAsStaffAsync("Moderator");

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/admin/countries/{Country}/featured",
            new { guideId = foreign },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await response.Content.ReadAsStringAsync()).Should().Contain(
            "knowledge.guide.country-mismatch",
            "the client switches on the code, so it must be the frozen one");
    }

    [Fact]
    public async Task A_guide_that_is_not_published_cannot_be_featured()
    {
        const string Country = "ZM";

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var draft = await ReadDataAsync<GuideDto>(
            await HttpClient.PostAsJsonAsync(
                GuidesRoot,
                new { title = "An unfinished draft", categoryKey = "immigration", language = "en", countryCode = Country },
                JsonOptions));

        await OpenAndPublishCountryAsync(Country, "Zambia", "immigration");

        await AuthenticateAsStaffAsync("Moderator");

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/admin/countries/{Country}/featured",
            new { guideId = draft.GuideId },
            JsonOptions);

        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest, "featuring a draft would put a dead link on the country page");
    }

    /// <summary>
    /// An entry pointing at a row that does not exist renders as a raw key on the most-read screen
    /// in the product. Checking where the curator is present is the only moment the mistake is cheap.
    /// </summary>
    [Fact]
    public async Task An_arrangement_naming_a_category_that_does_not_exist_is_refused()
    {
        const string Country = "SN";

        await AuthenticateAsStaffAsync("Admin");

        await HttpClient.PostAsJsonAsync(
            $"{Root}/admin/countries/{Country}", new { displayName = "Senegal" }, JsonOptions);

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/admin/countries/{Country}/arrangement",
            new { entries = new[] { new { knowledgeCategoryKey = "no-such-row" } } },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Archiving a row a published country still lists would silently empty a line on that page —
    /// the entry would resolve to nothing, the composer would drop it, and nobody could account for
    /// the gap.
    /// </summary>
    [Fact]
    public async Task A_navigation_row_a_country_still_lists_cannot_be_archived()
    {
        const string Country = "BW";

        await AuthenticateAsStaffAsync("Admin");

        var created = await ReadDataAsync<CategoryDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/categories",
                new { key = "in-use-row", name = "In use", sourceCategoryKeys = new[] { "other" } },
                JsonOptions));

        created.Key.Should().Be("in-use-row");

        await HttpClient.PostAsJsonAsync(
            $"{Root}/admin/countries/{Country}", new { displayName = "Botswana" }, JsonOptions);

        await HttpClient.PutAsJsonAsync(
            $"{Root}/admin/countries/{Country}/arrangement",
            new { entries = new[] { new { knowledgeCategoryKey = "in-use-row" } } },
            JsonOptions);

        var refused = await HttpClient.DeleteAsync($"{Root}/categories/in-use-row");

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("knowledge.category.in-use");

        // Remove it from the country, and the archival goes through.
        await HttpClient.PutAsJsonAsync(
            $"{Root}/admin/countries/{Country}/arrangement",
            new { entries = Array.Empty<object>() },
            JsonOptions);

        (await HttpClient.DeleteAsync($"{Root}/categories/in-use-row"))
            .IsSuccessStatusCode.Should().BeTrue();
    }

    /// <summary>
    /// <b>The undelete blind spot, closed.</b> Archiving is a soft delete, so the global query filter
    /// hides the row — and a restore has to load exactly the row the archiving hid. Without the
    /// repository member that ignores the filter, this answers "not found" for a row the curator can
    /// see in their own archive list.
    /// </summary>
    [Fact]
    public async Task An_archived_navigation_row_can_be_restored()
    {
        await AuthenticateAsStaffAsync("Admin");

        await HttpClient.PostAsJsonAsync(
            $"{Root}/categories",
            new { key = "temporary-row", name = "Temporary" },
            JsonOptions);

        (await HttpClient.DeleteAsync($"{Root}/categories/temporary-row"))
            .IsSuccessStatusCode.Should().BeTrue();

        var active = await ReadDataAsync<IReadOnlyList<CategoryDto>>(
            await HttpClient.GetAsync($"{Root}/categories"));

        active.Should().NotContain(category => category.Key == "temporary-row");

        var archived = await ReadDataAsync<IReadOnlyList<CategoryDto>>(
            await HttpClient.GetAsync($"{Root}/categories?includeArchived=true"));

        archived.Should().Contain(
            category => category.Key == "temporary-row",
            "a curator has to be able to see what they archived");

        (await HttpClient.PostAsync($"{Root}/categories/temporary-row/restore", null))
            .IsSuccessStatusCode.Should().BeTrue();

        (await ReadDataAsync<IReadOnlyList<CategoryDto>>(
                await HttpClient.GetAsync($"{Root}/categories")))
            .Should().Contain(category => category.Key == "temporary-row");
    }

    /// <summary>
    /// Curation is the one part of this platform where simultaneous editors are the norm — a small
    /// team, all at once, during the week a country launches.
    /// </summary>
    [Fact]
    public async Task A_second_curator_working_from_stale_state_is_told_rather_than_overwriting()
    {
        const string Country = "UG";

        await AuthenticateAsStaffAsync("Admin");

        var created = await ReadDataAsync<DirectoryDetailDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/admin/countries/{Country}", new { displayName = "Uganda" }, JsonOptions));

        await HttpClient.PutAsJsonAsync(
            $"{Root}/admin/countries/{Country}/arrangement",
            new { entries = new[] { new { knowledgeCategoryKey = "immigration" } }, revision = created.Revision },
            JsonOptions);

        var conflict = await HttpClient.PutAsJsonAsync(
            $"{Root}/admin/countries/{Country}/arrangement",
            new { entries = new[] { new { knowledgeCategoryKey = "housing" } }, revision = created.Revision },
            JsonOptions);

        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await conflict.Content.ReadAsStringAsync()).Should().Contain(
            "knowledge.revision-conflict",
            "a curation UI offers a reload from this code rather than an error page");
    }

    // ── Learning paths ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_learning_path_is_written_published_and_walked()
    {
        const string Country = "ZA";

        var guideId = await PublishGuideAsync("Getting a South African work visa", "immigration", Country);

        await OpenAndPublishCountryAsync(Country, "South Africa", "immigration", "housing");

        await AuthenticateAsStaffAsync("Moderator");

        var created = await ReadDataAsync<LearningPathDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/admin/countries/{Country}/paths",
                new
                {
                    key = "arriving",
                    title = "Arriving in South Africa",
                    description = "Seven things to do in your first month.",
                    language = "en"
                },
                JsonOptions));

        created.Status.Should().Be("draft", "a path is written over days and must not be half-visible");
        created.Steps.Should().BeEmpty();

        // A path with no steps cannot be published.
        (await HttpClient.PostAsync(
                $"{Root}/admin/countries/{Country}/paths/arriving/publish", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var housingStepId = Guid.NewGuid();

        var arranged = await ReadDataAsync<LearningPathDto>(
            await HttpClient.PutAsJsonAsync(
                $"{Root}/admin/countries/{Country}/paths/arriving/steps",
                new
                {
                    steps = new object[]
                    {
                        new
                        {
                            stepId = Guid.NewGuid(),
                            title = "Sort out your right to be here",
                            guideId,
                            knowledgeCategoryKey = "immigration"
                        },
                        new
                        {
                            stepId = housingStepId,
                            title = "Find somewhere to live",
                            knowledgeCategoryKey = "housing"
                        },
                        new
                        {
                            stepId = Guid.NewGuid(),
                            title = "Register the children for school",
                            knowledgeCategoryKey = "education",
                            isOptional = true
                        }
                    },
                    revision = created.Revision
                },
                JsonOptions));

        arranged.StepCount.Should().Be(3);
        arranged.RequiredStepCount.Should().Be(
            2, "an optional step must not tell a single graduate they are permanently 6 of 7 done");

        arranged.Steps[0].Title.Should().Be(
            "Sort out your right to be here",
            "the step's own framing, not the guide's title — they read very differently");

        arranged.Steps[0].Guide.Should().NotBeNull("the step's guide is resolved for the client");

        (await HttpClient.PostAsync(
                $"{Root}/admin/countries/{Country}/paths/arriving/publish", null))
            .IsSuccessStatusCode.Should().BeTrue();

        // ── A step identifier survives an insertion above it ────────────────────────────────
        var reread = await ReadDataAsync<LearningPathDto>(
            await HttpClient.GetAsync($"{Root}/countries/{Country}/paths/arriving"));

        var rearranged = await ReadDataAsync<LearningPathDto>(
            await HttpClient.PutAsJsonAsync(
                $"{Root}/admin/countries/{Country}/paths/arriving/steps",
                new
                {
                    steps = new object[]
                    {
                        new { stepId = Guid.NewGuid(), title = "Read this first", guideId },
                        new
                        {
                            stepId = housingStepId,
                            title = "Find somewhere to live",
                            knowledgeCategoryKey = "housing"
                        }
                    },
                    revision = reread.Revision
                },
                JsonOptions));

        rearranged.Status.Should().Be(
            "published",
            "rearranging a live path must not take a country's onboarding offline: the commonest " +
            "edit is inserting a step somebody discovered was missing");

        rearranged.Steps.Single(step => step.StepId == housingStepId).Position.Should().Be(
            1, "it moved down, and a client tracking progress still recognises it");

        // ── A reader sees it ────────────────────────────────────────────────────────────────
        Api.ClearAuthentication();

        var listed = await ReadDataAsync<IReadOnlyList<LearningPathSummaryDto>>(
            await HttpClient.GetAsync($"{Root}/countries/{Country}/paths"));

        listed.Should().ContainSingle().Which.Key.Should().Be("arriving");

        var page = await ReadDataAsync<CountryDirectoryDto>(
            await HttpClient.GetAsync($"{Root}/countries/{Country}"));

        page.LearningPaths.Should().ContainSingle(
            "the country page lists its onboarding routes without their steps");
    }

    [Fact]
    public async Task A_draft_learning_path_is_invisible_to_a_reader()
    {
        const string Country = "ET";

        await OpenAndPublishCountryAsync(Country, "Ethiopia", "immigration");

        await AuthenticateAsStaffAsync("Moderator");

        await HttpClient.PostAsJsonAsync(
            $"{Root}/admin/countries/{Country}/paths",
            new { key = "unfinished", title = "Not ready yet" },
            JsonOptions);

        (await ReadAnonymouslyAsync($"{Root}/countries/{Country}/paths/unfinished"))
            .Should().Be(
                HttpStatusCode.NotFound,
                "a newcomer following a link to a half-written sequence is the failure the draft " +
                "state exists to prevent");
    }

    // ── Discovery and search ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_finds_a_guide_and_counts_the_phrase_that_found_it()
    {
        const string Country = "NA";

        await PublishGuideAsync("Registering a Namibian company", "business", Country);
        await OpenAndPublishCountryAsync(Country, "Namibia", "business");

        Api.ClearAuthentication();

        var found = await ReadDataAsync<SearchDto>(
            await HttpClient.GetAsync($"{Root}/search?countryCode={Country}&q=Namibian"));

        found.Results.Should().ContainSingle();
        found.CountryCode.Should().Be(Country, "the scope is echoed so a client can tell them apart");
        found.Engine.Should().NotBeNullOrWhiteSpace();

        // A phrase that finds nothing is the platform being told what to commission next.
        var missed = await ReadDataAsync<SearchDto>(
            await HttpClient.GetAsync(
                $"{Root}/search?countryCode={Country}&q=how%20to%20import%20a%20racehorse"));

        missed.Results.Should().BeEmpty();

        await AuthenticateAsStaffAsync("Moderator");

        var unanswered = await ReadDataAsync<IReadOnlyList<TrendingDto>>(
            await HttpClient.GetAsync(
                $"{Root}/countries/{Country}/trending?unansweredOnly=true"));

        unanswered.Should().Contain(
            topic => topic.Term == "how to import a racehorse" && topic.IsUnanswered,
            "an unanswered phrase is a guide that should be written, named in the words somebody used");
    }

    [Fact]
    public async Task The_shelves_answer_and_the_curated_one_keeps_its_curated_order()
    {
        const string Country = "CM";

        var first = await PublishGuideAsync("First Cameroonian guide", "banking", Country);
        var second = await PublishGuideAsync("Second Cameroonian guide", "banking", Country);

        await OpenAndPublishCountryAsync(Country, "Cameroon", "banking");

        await AuthenticateAsStaffAsync("Moderator");

        // Featured second, then first inserted above it.
        await HttpClient.PostAsJsonAsync(
            $"{Root}/admin/countries/{Country}/featured", new { guideId = second }, JsonOptions);

        await HttpClient.PostAsJsonAsync(
            $"{Root}/admin/countries/{Country}/featured",
            new { guideId = first, position = 0 },
            JsonOptions);

        Api.ClearAuthentication();

        var featured = await ReadDataAsync<PagedDto<GuideCardDto>>(
            await HttpClient.GetAsync($"{Root}/featured?countryCode={Country}"));

        featured.Items.Select(card => card.GuideId).Should().BeEquivalentTo(
            new[] { first, second },
            options => options.WithStrictOrdering(),
            "the curated order is the answer — re-querying would reorder it by whatever the query ordered by");

        var recent = await ReadDataAsync<PagedDto<GuideCardDto>>(
            await HttpClient.GetAsync($"{Root}/recently-updated?countryCode={Country}"));

        recent.Items.Should().HaveCount(2);

        var official = await ReadDataAsync<PagedDto<GuideCardDto>>(
            await HttpClient.GetAsync($"{Root}/official?countryCode={Country}"));

        official.Items.Should().BeEmpty(
            "official is narrower than verified: it needs an authority badge from Trust");
    }

    [Fact]
    public async Task Recommended_next_reads_are_curated_and_resolved()
    {
        const string Country = "CI";

        var source = await PublishGuideAsync("Starting point", "getting-started", Country);
        var next = await PublishGuideAsync("What comes after", "housing", Country);

        await AuthenticateAsStaffAsync("Moderator");

        (await HttpClient.PutAsJsonAsync(
                $"{Root}/admin/guides/{source}/next",
                new
                {
                    reads = new[]
                    {
                        new { guideId = next, reason = "Read this before you sign anything." }
                    }
                },
                JsonOptions))
            .IsSuccessStatusCode.Should().BeTrue();

        Api.ClearAuthentication();

        var reads = await ReadDataAsync<IReadOnlyList<RecommendedReadDto>>(
            await HttpClient.GetAsync($"{Root}/guides/{source}/next"));

        reads.Should().ContainSingle();
        reads[0].Guide.GuideId.Should().Be(next);
        reads[0].Reason.Should().Be(
            "Read this before you sign anything.",
            "the sentence a person wrote is the whole difference from a computed recommendation");
    }

    [Fact]
    public async Task A_guide_cannot_be_recommended_as_the_next_read_after_itself()
    {
        var guideId = await PublishGuideAsync("A guide that points at itself", "other", "TG");

        await AuthenticateAsStaffAsync("Moderator");

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/admin/guides/{guideId}/next",
            new { reads = new[] { new { guideId } } },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Analytics ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The client reports what the server cannot see.</b> Opening a country page is a request the
    /// server observes; tapping through to a guide from a shelf is a request to a different endpoint
    /// that carries no memory of where the reader came from.
    /// </summary>
    [Fact]
    public async Task A_country_page_counts_its_readers_and_the_guides_they_went_on_to_open()
    {
        const string Country = "MW";

        var guideId = await PublishGuideAsync("Malawian residence permits", "immigration", Country);
        await OpenAndPublishCountryAsync(Country, "Malawi", "immigration");

        Api.ClearAuthentication();

        await HttpClient.GetAsync($"{Root}/countries/{Country}");
        await HttpClient.GetAsync($"{Root}/countries/{Country}");
        await HttpClient.GetAsync($"{Root}/countries/{Country}/categories/immigration");

        (await HttpClient.PostAsJsonAsync(
                $"{Root}/countries/{Country}/activity/discovered",
                new { guideId, knowledgeCategoryKey = "immigration" },
                JsonOptions))
            .IsSuccessStatusCode.Should().BeTrue();

        var statistics = await ReadDataAsync<StatisticsDto>(
            await HttpClient.GetAsync($"{Root}/countries/{Country}/statistics"));

        statistics.DirectoryViews.Should().BeGreaterThanOrEqualTo(2);
        statistics.CategoryViews.Should().BeGreaterThanOrEqualTo(1);
        statistics.GuideDiscoveries.Should().BeGreaterThanOrEqualTo(1);

        statistics.DiscoveryRate.Should().BeGreaterThan(
            0, "the one number that says whether the directory is doing its job");
    }

    [Fact]
    public async Task An_activity_signal_for_a_country_that_is_not_one_is_refused()
    {
        Api.ClearAuthentication();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/countries/XYZ/activity/discovered", new { }, JsonOptions);

        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            "silently discarding it would make a client's analytics quietly stop working");
    }

    // ── Cross-module integration ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The proof that the dashboard's contextual band is fed by a published contract and not by a
    /// module reference.</b> A job is posted through B9's own API; the Knowledge Directory learns
    /// about it only when the outbox delivers <c>JobPublishedIntegrationEvent</c>, and the country's
    /// jobs counter moves.
    /// </summary>
    [Fact]
    public async Task A_job_published_elsewhere_reaches_the_country_dashboard_through_the_outbox()
    {
        const string Country = "SL";

        await OpenAndPublishCountryAsync(Country, "Sierra Leone", "employment");

        await Factory.DrainOutboxAsync();

        Api.ClearAuthentication();

        var before = await ReadDataAsync<DashboardDto>(
            await HttpClient.GetAsync($"{Root}/countries/{Country}/dashboard"));

        before.Context.Should().NotContain(
            block => block.Kind == "jobs", "nothing has been posted for this country yet");

        await PublishJobContractAsync(Country);

        // Nothing has reached the directory yet: the contract is durable and undelivered.
        var pending = await Factory.PendingOutboxAsync();

        pending.Should().Contain(
            message => message.EventType.Contains("JobPublished", StringComparison.Ordinal),
            "the vertical announced itself through the transactional outbox");

        await Factory.DrainOutboxAsync();

        (await Factory.DeadLettersAsync())
            .Where(message => message.EventType.Contains("JobPublished", StringComparison.Ordinal))
            .Should().BeEmpty("the directory's handler accepted the contract");

        var after = await ReadDataAsync<DashboardDto>(
            await HttpClient.GetAsync($"{Root}/countries/{Country}/dashboard"));

        after.Context.Should().Contain(
            block => block.Kind == "jobs" && block.Count >= 1,
            "the counter is maintained from a published contract, with no reference to the Jobs module");

        after.Context.Single(block => block.Kind == "jobs").Route.Should().NotBeNullOrWhiteSpace(
            "the band says how much there is and where to go; the owning vertical answers what it is");
    }

    /// <summary>
    /// Curation announces itself through the transactional outbox rather than an in-process call, so
    /// a consumer holding a rendered country page finds out it is stale.
    /// </summary>
    [Fact]
    public async Task Publishing_a_country_leaves_a_durable_message_the_outbox_delivers()
    {
        const string Country = "BJ";

        await Factory.DrainOutboxAsync();

        await OpenAndPublishCountryAsync(Country, "Benin", "immigration");

        var pending = await Factory.PendingOutboxAsync();

        pending.Should().Contain(
            message => message.EventType.Contains(
                "CountryKnowledgeDirectoryUpdated", StringComparison.Ordinal),
            "arranging and publishing a country must leave a committed promise to announce it");

        pending.Where(message => message.EventType.Contains("Knowledge", StringComparison.Ordinal))
            .Should().OnlyContain(
                message => message.EventType.StartsWith(
                    "Diaspora.Contracts.", StringComparison.Ordinal),
                "the stored identity is a contract name, never a CLR assembly-qualified type");

        (await Factory.DrainOutboxAsync()).Should().BeGreaterThan(0);

        (await Factory.DeadLettersAsync())
            .Where(message => message.EventType.Contains("Knowledge", StringComparison.Ordinal))
            .Should().BeEmpty("no consumer rejected a message the directory produced");
    }

    // ── Localization ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_country_page_answers_in_the_readers_locale_and_falls_back_when_it_cannot()
    {
        const string Country = "DZ";

        await AuthenticateAsStaffAsync("Admin");

        var created = await ReadDataAsync<DirectoryDetailDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/admin/countries/{Country}",
                new { displayName = "Algeria", primaryLanguage = "ar" },
                JsonOptions));

        await HttpClient.PutAsJsonAsync(
            $"{Root}/admin/countries/{Country}",
            new
            {
                displayName = "Algeria",
                primaryLanguage = "ar",
                localizations = new[] { new { locale = "fr", name = "Algerie", description = "Guide du pays." } },
                revision = created.Revision
            },
            JsonOptions);

        await HttpClient.PutAsJsonAsync(
            $"{Root}/admin/countries/{Country}/arrangement",
            new { entries = new[] { new { knowledgeCategoryKey = "immigration" } } },
            JsonOptions);

        await HttpClient.PostAsync($"{Root}/admin/countries/{Country}/publish", null);

        Api.ClearAuthentication();

        var french = await ReadDataAsync<CountryDirectoryDto>(
            await HttpClient.GetAsync($"{Root}/countries/{Country}?locale=fr-DZ"));

        french.DisplayName.Should().Be(
            "Algerie",
            "a reader asking for Algerian French gets plain French — right far more often than English");

        var swahili = await ReadDataAsync<CountryDirectoryDto>(
            await HttpClient.GetAsync($"{Root}/countries/{Country}?locale=sw"));

        swahili.DisplayName.Should().Be("Algeria", "nobody wrote a Swahili header");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Publishes a guide as a verified member and returns its identifier.</summary>
    private async Task<Guid> PublishGuideAsync(string title, string categoryKey, string countryCode)
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var draft = await ReadDataAsync<GuideDto>(
            await HttpClient.PostAsJsonAsync(
                GuidesRoot,
                new { title, categoryKey, language = "en", countryCode },
                JsonOptions));

        await HttpClient.PutAsJsonAsync(
            $"{GuidesRoot}/{draft.GuideId}",
            new
            {
                title,
                countryCode,
                sections = new[]
                {
                    new
                    {
                        sectionId = Guid.NewGuid(),
                        title = "What to do",
                        order = 0,
                        blocks = new[]
                        {
                            new
                            {
                                blockId = Guid.NewGuid(),
                                kind = "Markdown",
                                order = 0,
                                content = "Take the form, the fee and one hour of patience."
                            }
                        }
                    }
                }
            },
            JsonOptions);

        var published = await ReadDataAsync<GuideDto>(
            await HttpClient.PostAsJsonAsync(
                $"{GuidesRoot}/{draft.GuideId}/publish",
                new { revisionNotes = "First edition." },
                JsonOptions));

        published.Status.Should().Be("Published");

        return published.GuideId;
    }

    /// <summary>
    /// Publishes B9's contract through the platform's own publisher, exactly as the Jobs module
    /// does.
    /// <para>
    /// <b>The contract rather than B9's request shape, deliberately.</b> What is under test here is
    /// B8.1's half of the integration: that a published <c>JobPublishedIntegrationEvent</c> becomes a
    /// durable outbox row, reaches this module's handler and moves a country counter. Driving B9's
    /// own endpoints instead would couple this suite to the Jobs module's request contract — which
    /// B9's suite already covers — and would make a change over there fail a test about knowledge.
    /// </para>
    /// </summary>
    private async Task PublishJobContractAsync(string countryCode)
    {
        using var scope = Factory.Services.CreateScope();

        var publisher = scope.ServiceProvider
            .GetRequiredService<Diaspora.Platform.Messaging.IIntegrationEventPublisher>();

        await publisher.PublishAsync(new JobPublishedIntegrationEvent
        {
            JobId = Guid.NewGuid(),
            EmployerId = Guid.NewGuid(),
            EmployerName = "Freetown Logistics",
            IsEmployerVerified = true,
            Title = "Warehouse supervisor",
            Slug = $"warehouse-supervisor-{Guid.NewGuid():N}",
            CategoryKey = "logistics",
            Tags = [],
            SkillKeys = [],
            CountryCode = countryCode,
            WorkplaceType = "OnSite",
            JobType = "FullTime",
            ExperienceLevel = "Mid",
            OffersVisaSponsorship = false,
            OffersRelocationAssistance = false,
            IsSalaryDisclosed = false,
            VacancyCount = 1,
            IsFirstPublication = true,
            PublishedAt = DateTimeOffset.UtcNow
        });

        // The publisher writes an outbox row inside the ambient unit of work; nothing downstream is
        // going to save for a test.
        await scope.ServiceProvider
            .GetRequiredService<Diaspora.Domain.Abstractions.IUnitOfWork>()
            .SaveChangesAsync();
    }

    /// <summary>Opens a country, arranges the rows given, and publishes it. Leaves the caller as staff.</summary>
    private async Task OpenAndPublishCountryAsync(
        string countryCode, string displayName, params string[] categoryKeys)
    {
        await AuthenticateAsStaffAsync("Admin");

        await HttpClient.PostAsJsonAsync(
            $"{Root}/admin/countries/{countryCode}", new { displayName }, JsonOptions);

        if (categoryKeys.Length > 0)
        {
            await HttpClient.PutAsJsonAsync(
                $"{Root}/admin/countries/{countryCode}/arrangement",
                new
                {
                    entries = categoryKeys.Select(key => new { knowledgeCategoryKey = key }).ToArray()
                },
                JsonOptions);
        }

        await HttpClient.PostAsync($"{Root}/admin/countries/{countryCode}/publish", null);
    }

    /// <summary>
    /// Registers a member, grants them a platform role, and signs them in again so the token carries
    /// it. <see cref="IdentityTestBase"/> offers no staff helper, and the role is assigned through the
    /// aggregate rather than by writing a column.
    /// </summary>
    private async Task AuthenticateAsStaffAsync(string roleName)
    {
        var (phone, auth) = await RegisterVerifiedMemberAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

            var userId = new UserId(auth.User.Id);

            var user = await dbContext.Set<User>()
                .FirstOrDefaultAsync(candidate => candidate.Id == userId);

            user.Should().NotBeNull();

            user!.AssignRole(UserRole.All.Single(role => role.Name == roleName));

            await dbContext.SaveChangesAsync();
        }

        var login = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(
            await Api.LoginAsync(new LoginRequest { Email = EmailFor(phone), Password = ValidPassword }));

        Api.Authenticate(login.Tokens.AccessToken);
    }

    /// <summary>Reads with no credential at all, restoring the caller's authentication afterwards.</summary>
    private async Task<HttpStatusCode> ReadAnonymouslyAsync(string route)
    {
        var authorization = HttpClient.DefaultRequestHeaders.Authorization;

        try
        {
            Api.ClearAuthentication();

            return (await HttpClient.GetAsync(route)).StatusCode;
        }
        finally
        {
            HttpClient.DefaultRequestHeaders.Authorization = authorization;
        }
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

    // ── Wire shapes, re-declared so the tests are a contract rather than a compilation ──────

    private sealed record Envelope<T>(T Data);

    private sealed record PagedDto<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);

    private sealed record CategoryDto(
        string Key,
        string Name,
        bool IsActive,
        int Revision,
        IReadOnlyList<string> SourceCategoryKeys,
        IReadOnlyList<string> ProviderCategoryKeys);

    private sealed record DirectoryEntryDto(
        string KnowledgeCategoryKey, string Label, int Position, int GuideCount, bool IsHidden);

    private sealed record DirectoryDetailDto(
        string CountryCode,
        string DisplayName,
        bool IsPublished,
        int Revision,
        IReadOnlyList<DirectoryEntryDto> Entries);

    private sealed record CountryDirectoryDto(
        string CountryCode,
        string DisplayName,
        bool IsPublished,
        IReadOnlyList<string> Languages,
        IReadOnlyList<DirectoryEntryDto> Categories,
        IReadOnlyList<GuideCardDto> FeaturedGuides,
        IReadOnlyList<LearningPathSummaryDto> LearningPaths,
        StatisticsDto Statistics);

    private sealed record SectionDto(string Key, string Title, IReadOnlyList<GuideCardDto> Guides);

    private sealed record CategoryViewDto(
        string CountryCode,
        string CountryName,
        string? Label,
        int GuideCount,
        IReadOnlyList<SectionDto> Sections,
        IReadOnlyList<string> ProviderCategoryKeys);

    private sealed record ContextBlockDto(string Kind, string Label, int Count, string? Route);

    private sealed record DashboardDto(
        string CountryCode,
        string DisplayName,
        StatisticsDto Statistics,
        IReadOnlyList<GuideCardDto> Featured,
        IReadOnlyList<GuideCardDto> RecentlyUpdated,
        IReadOnlyList<CategoryCountDto> CategoryBreakdown,
        IReadOnlyList<TrendingDto> TrendingTopics,
        IReadOnlyList<ContextBlockDto> Context);

    private sealed record CategoryCountDto(
        string KnowledgeCategoryKey, string Name, int GuideCount, long Views);

    private sealed record StatisticsDto(
        string CountryCode,
        int TotalGuides,
        int VerifiedGuides,
        int OfficialGuides,
        int CategoryCount,
        long DirectoryViews,
        long CategoryViews,
        long GuideDiscoveries,
        double DiscoveryRate);

    private sealed record TrendingDto(
        string Term, long SearchCount, int ResultCount, bool IsUnanswered);

    private sealed record SearchDto(
        IReadOnlyList<GuideCardDto> Results, bool HasMore, string Engine, string? CountryCode);

    private sealed record LearningPathStepDto(
        Guid StepId, int Position, string Title, bool IsOptional, GuideCardDto? Guide);

    private sealed record LearningPathDto(
        Guid LearningPathId,
        string Key,
        string CountryCode,
        string Title,
        string Status,
        int StepCount,
        int RequiredStepCount,
        int Revision,
        IReadOnlyList<LearningPathStepDto> Steps);

    private sealed record LearningPathSummaryDto(
        Guid LearningPathId, string Key, string Title, string Status, int StepCount);

    private sealed record RecommendedReadDto(int Position, string? Reason, GuideCardDto Guide);

    private sealed record CapabilitiesDto(
        IReadOnlyList<string> Sections,
        IReadOnlyList<string> Sorts,
        IReadOnlyList<string> Difficulties,
        string SearchEngine,
        bool SearchAvailable,
        string CacheProvider,
        bool CachingEnabled,
        IReadOnlyList<string> ContextModules,
        IReadOnlyList<string> AnalyticsSinks,
        int CategoryCount,
        int PublishedCountryCount,
        int MaxPathSteps);

    private sealed record GuideCardDto(Guid GuideId, string Slug, string Title, string CountryCode);

    private sealed record GuideDto(Guid GuideId, string Status, int Revision);

}
