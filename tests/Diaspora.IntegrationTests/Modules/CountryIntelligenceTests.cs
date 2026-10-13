using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Contracts.Intelligence.V1.Events;
using Diaspora.Domain.Identity.Users;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The Country Intelligence Platform (B8.2), driven over real HTTP through the same host every other
/// module suite uses.
/// <para>
/// Four claims are under test at once. That the platform composed in through nothing but contributor
/// interfaces — a third permission contributor, a third endpoint contributor, a third telemetry
/// source, twenty-three tables in B8's own schema — with no edit to the kernel, to Identity, or to
/// <c>Program.cs</c>. That the editorial workflow holds end to end: draft, submit, verify, publish,
/// supersede, archive, restore. That the two gates the word "official" rests on cannot be bypassed —
/// an unverified authority cannot publish, and an uncited source is refused. And that the whole thing
/// is <b>readable by anybody</b>, because an emergency number that requires an account is an
/// emergency number nobody reaches.
/// </para>
/// <para>
/// The wire shapes below are re-declared by hand rather than referenced from the module. Binding the
/// tests to B8.2's own response records would make them pass whenever the module compiles, which is
/// the one thing a contract test must never do.
/// </para>
/// </summary>
public sealed class CountryIntelligenceTests : IdentityTestBase
{
    private const string Root = "/api/v1/intelligence";
    private const string KnowledgeRoot = "/api/v1/knowledge";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CountryIntelligenceTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Composition ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_notice_catalogue_was_seeded_by_the_modules_own_hosted_service()
    {
        Api.ClearAuthentication();

        var categories = await ReadDataAsync<IReadOnlyList<CategoryDto>>(
            await HttpClient.GetAsync($"{Root}/categories"));

        categories.Should().NotBeEmpty(
            "a platform with no notice categories cannot file a single notice");

        categories.Select(category => category.Key).Should().Contain(
            [
                "emergency-alerts",
                "visa-advisories",
                "government-announcements",
                "public-holidays",
                "embassy-notices"
            ]);

        // The alert family is what makes a deployment able to reclassify a category without a
        // release. It has to survive the round trip.
        categories.Single(category => category.Key == "emergency-alerts")
            .AlertFamily.Should().Be("Emergency");

        categories.Single(category => category.Key == "government-announcements")
            .AlertFamily.Should().Be("GovernmentAnnouncement");

        categories.Single(category => category.Key == "public-holidays")
            .AlertFamily.Should().Be("None");
    }

    [Fact]
    public async Task The_capabilities_endpoint_reports_the_seams_this_deployment_registered()
    {
        Api.ClearAuthentication();

        var capabilities = await ReadDataAsync<CapabilitiesDto>(
            await HttpClient.GetAsync($"{Root}/capabilities"));

        capabilities.SearchEngine.Should().Be("ef");
        capabilities.SearchAvailable.Should().BeTrue();
        capabilities.AuthorityVerificationAvailable.Should().BeTrue(
            "Trust is loaded, so the shipped provider can raise a claim");

        capabilities.SourceValidators.Should().NotBeEmpty(
            "the shipped citation rule is a validator like any other");

        capabilities.AnalyticsSinks.Should().Contain("country-counters");

        // Ascending by urgency rather than alphabetically, which is the whole reason the rank
        // column exists.
        capabilities.Severities.Should().ContainInOrder(
            "Informational", "Low", "Medium", "High", "Critical");

        capabilities.EmergencyKinds.Select(kind => kind.Key).Should().Contain(
            ["police", "fire", "ambulance", "mental-health"]);

        capabilities.EmergencyKinds.Where(kind => kind.IsEssential)
            .Select(kind => kind.Key)
            .Should().BeEquivalentTo(["police", "fire", "ambulance"]);
    }

    [Fact]
    public async Task Reading_official_information_requires_no_account()
    {
        Api.ClearAuthentication();

        foreach (var route in new[]
        {
            $"{Root}/categories",
            $"{Root}/capabilities",
            $"{Root}/notices",
            $"{Root}/embassies",
            $"{Root}/search?q=visa"
        })
        {
            var response = await HttpClient.GetAsync(route);

            response.StatusCode.Should().NotBe(
                HttpStatusCode.Unauthorized,
                "reading what a government announced needs no account: {0}", route);
        }
    }

    [Fact]
    public async Task Every_write_is_refused_to_an_anonymous_caller()
    {
        Api.ClearAuthentication();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/authorities",
            new
            {
                countryCode = "MU",
                name = "Ministry of Nothing",
                authorityType = "Other"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── The editorial workflow ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The gate the whole phase rests on.</b> A notice cannot be published unless the body that
    /// issued it has been verified — and no configuration switches that off.
    /// </summary>
    [Fact]
    public async Task A_notice_from_an_unverified_authority_cannot_be_published()
    {
        await AuthenticateAsStaffAsync("Admin");

        var authority = await CreateAuthorityAsync("MU", "Unverified Office");
        var notice = await CreateNoticeAsync("MU", authority.IssuingAuthorityId);

        await PostAsync($"{Root}/notices/{notice.Card.OfficialNoticeId}/submit");
        await VerifyAsync(notice.Card.OfficialNoticeId, approved: true);

        var published = await HttpClient.PostAsync(
            $"{Root}/notices/{notice.Card.OfficialNoticeId}/publish", content: null);

        published.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await published.Content.ReadAsStringAsync())
            .Should().Contain("intelligence.authority.unverified");
    }

    /// <summary>
    /// <b>The one source rule that holds in every jurisdiction.</b> An official notice cites
    /// something — a URL or a reference number — because a notice that names no source at all is a
    /// claim rather than an announcement.
    /// </summary>
    [Fact]
    public async Task A_notice_with_no_cited_source_cannot_be_published()
    {
        await AuthenticateAsStaffAsync("Admin");

        var authority = await CreateVerifiedAuthorityAsync("MU", "Cited Source Ministry");
        var notice = await CreateNoticeAsync("MU", authority);

        await PostAsync($"{Root}/notices/{notice.Card.OfficialNoticeId}/submit");
        await VerifyAsync(notice.Card.OfficialNoticeId, approved: true);

        var published = await HttpClient.PostAsync(
            $"{Root}/notices/{notice.Card.OfficialNoticeId}/publish", content: null);

        published.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await published.Content.ReadAsStringAsync())
            .Should().Contain("intelligence.source.rejected");
    }

    // ── Written by Diaspora (decided 7 Oct 2026) ─────────────────────────────────────────────
    //
    // Notices, visa advisories and embassy details are written by platform admins in the console
    // and shown as "Posted by Diaspora platform admin". The platform's own authority makes that
    // possible without pretending to be a government: confirmed at creation, never official.

    [Fact]
    public async Task The_platform_authority_is_made_once_per_country_and_confirmed()
    {
        await AuthenticateAsStaffAsync("Admin");

        var first = await ReadDataAsync<AuthorityDto>(
            await PostAsync($"{Root}/countries/MU/platform-authority"));
        var second = await ReadDataAsync<AuthorityDto>(
            await PostAsync($"{Root}/countries/MU/platform-authority"));

        second.IssuingAuthorityId.Should().Be(first.IssuingAuthorityId, "it is idempotent");
        first.AuthorityType.Should().Be("Platform");
        first.Name.Should().Be("Diaspora platform");
        first.IsVerified.Should().BeTrue("nothing has to confirm Diaspora to itself");
    }

    [Fact]
    public async Task An_admin_notice_publishes_without_a_source_and_reads_as_posted_by_the_platform()
    {
        await AuthenticateAsStaffAsync("Admin");

        var platform = await ReadDataAsync<AuthorityDto>(
            await PostAsync($"{Root}/countries/MU/platform-authority"));
        var notice = await CreateNoticeAsync("MU", platform.IssuingAuthorityId,
            title: "Cyclone warning class 2");

        await PostAsync($"{Root}/notices/{notice.Card.OfficialNoticeId}/submit");
        await VerifyAsync(notice.Card.OfficialNoticeId, approved: true);

        var published = await ReadDataAsync<PlatformNoticeDto>(
            await PostAsync($"{Root}/notices/{notice.Card.OfficialNoticeId}/publish"));

        published.Card.Status.Should().Be("Published");
        published.Card.PostedByPlatform.Should().BeTrue();
        published.Card.IsOfficial.Should().BeFalse("an admin's notice is never shown as a government's");
        published.Card.AuthorityName.Should().Be("Diaspora platform");

        Api.ClearAuthentication();
        var read = await ReadDataAsync<PlatformNoticeDto>(
            await HttpClient.GetAsync($"{Root}/notices/{notice.Card.OfficialNoticeId}"));

        read.Card.PostedByPlatform.Should().BeTrue();
        read.Card.IsOfficial.Should().BeFalse();
    }

    [Fact]
    public async Task The_platform_type_cannot_be_claimed_through_the_authority_routes()
    {
        await AuthenticateAsStaffAsync("Admin");

        var response = await PostAsync(
            $"{Root}/authorities",
            new { countryCode = "MU", name = "Not Diaspora", authorityType = "Platform" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain("intelligence.authority.type-reserved");
    }

    [Fact]
    public async Task An_admin_visa_advisory_publishes_and_reads_as_posted_by_the_platform()
    {
        await AuthenticateAsStaffAsync("Admin");

        var platform = await ReadDataAsync<AuthorityDto>(
            await PostAsync($"{Root}/countries/MU/platform-authority"));
        var advisory = await CreateAdvisoryAsync("MU", null, platform.IssuingAuthorityId,
            "Visitor entry for everyone");

        var published = await ReadDataAsync<PlatformVisaDto>(
            await PostAsync($"{Root}/visas/{advisory}/publish"));

        published.Status.Should().Be("Published");
        published.PostedByPlatform.Should().BeTrue();
        published.IsOfficial.Should().BeFalse();
    }

    [Fact]
    public async Task A_notice_walks_draft_to_submitted_to_verified_to_published()
    {
        await AuthenticateAsStaffAsync("Admin");

        var authority = await CreateVerifiedAuthorityAsync("MU", "Immigration Office");
        var notice = await PublishableNoticeAsync("MU", authority);

        var submitted = await ReadDataAsync<NoticeDto>(
            await PostAsync($"{Root}/notices/{notice}/submit"));

        submitted.Card.Status.Should().Be("Submitted");
        submitted.Card.VerificationStatus.Should().Be("Pending");

        var verified = await VerifyAsync(notice, approved: true);

        verified.Card.Status.Should().Be("Verified");
        verified.Card.VerificationStatus.Should().Be("Verified");

        var published = await ReadDataAsync<NoticeDto>(
            await PostAsync($"{Root}/notices/{notice}/publish"));

        published.Card.Status.Should().Be("Published");
        published.Card.IsOfficial.Should().BeTrue();
        published.Card.IsActive.Should().BeTrue();
        published.Card.AuthorityName.Should().Be("Immigration Office");
    }

    /// <summary>
    /// A verifier confirmed a specific wording. The next wording is a different claim and has not
    /// been checked — so editing sends it back to draft rather than silently keeping the badge.
    /// </summary>
    [Fact]
    public async Task Editing_a_verified_notice_sends_it_back_to_draft()
    {
        await AuthenticateAsStaffAsync("Admin");

        var authority = await CreateVerifiedAuthorityAsync("MU", "Editing Ministry");
        var notice = await PublishableNoticeAsync("MU", authority);

        await PostAsync($"{Root}/notices/{notice}/submit");
        await VerifyAsync(notice, approved: true);

        var edited = await ReadDataAsync<NoticeDto>(
            await PutAsync(
                $"{Root}/notices/{notice}",
                new { title = "A completely different announcement" }));

        edited.Card.Status.Should().Be("Draft");
        edited.Card.VerificationStatus.Should().Be("Unverified");
    }

    /// <summary>
    /// Members react to and discuss a notice as they do an update, and cannot report it (13 Oct
    /// 2026). Before, a notice had no reactions, no comments and nothing to read them back from.
    /// </summary>
    [Fact]
    public async Task Members_react_to_and_comment_on_a_notice_but_cannot_report_it()
    {
        await AuthenticateAsStaffAsync("Admin");
        var authority = await CreateVerifiedAuthorityAsync("MU", "Engagement Ministry");
        var notice = await PublishedNoticeAsync("MU", authority);

        var (_, member) = await RegisterAndVerifyAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        var reacted = await HttpClient.PostAsJsonAsync(
            $"/api/v1/community/content/notice/{notice}/reactions", new { reactionKey = "helpful" });
        reacted.IsSuccessStatusCode.Should().BeTrue(await reacted.Content.ReadAsStringAsync());

        var commented = await HttpClient.PostAsJsonAsync(
            $"/api/v1/community/content/notice/{notice}/comments", new { body = "Is this open on Saturdays?" });
        commented.IsSuccessStatusCode.Should().BeTrue(await commented.Content.ReadAsStringAsync());

        var engagement = await HttpClient.GetStringAsync(
            $"/api/v1/community/content/notice/{notice}/engagement");
        using (var document = System.Text.Json.JsonDocument.Parse(engagement))
        {
            var data = document.RootElement.GetProperty("data");
            data.GetProperty("commentCount").GetInt32().Should().Be(1);
            data.GetProperty("reactions").GetProperty("helpfulCount").GetInt32().Should().Be(1);
            data.GetProperty("reactions").GetProperty("viewerReactions").EnumerateArray()
                .Select(key => key.GetString()).Should().Contain("helpful");
        }

        var reported = await HttpClient.PostAsJsonAsync(
            "/api/v1/community/reports",
            new { contentType = "notice", contentId = notice, reason = "misinformation" });
        reported.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await reported.Content.ReadAsStringAsync()).Should().Contain("community.report.not-reportable");
    }

    [Fact]
    public async Task A_published_notice_cannot_have_its_text_rewritten()
    {
        await AuthenticateAsStaffAsync("Admin");

        var authority = await CreateVerifiedAuthorityAsync("MU", "Frozen Ministry");
        var notice = await PublishedNoticeAsync("MU", authority);

        var response = await PutRawAsync(
            $"{Root}/notices/{notice}", new { title = "Rewritten after the fact" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await response.Content.ReadAsStringAsync())
            .Should().Contain("intelligence.notice.not-editable");
    }

    /// <summary>
    /// Presentation stays open while published: a translator finishing a version of a live advisory
    /// three days after it went out is the system working.
    /// </summary>
    [Fact]
    public async Task A_published_notice_still_accepts_a_translation()
    {
        await AuthenticateAsStaffAsync("Admin");

        var authority = await CreateVerifiedAuthorityAsync("MU", "Translating Ministry");
        var notice = await PublishedNoticeAsync("MU", authority);

        var translated = await ReadDataAsync<NoticeDto>(
            await PutAsync(
                $"{Root}/notices/{notice}",
                new
                {
                    localizations = new[]
                    {
                        new
                        {
                            locale = "fr",
                            title = "Nouvelles règles de visa",
                            summary = "Les règles changent le 1er avril.",
                            content = "Texte intégral de la circulaire."
                        }
                    }
                }));

        translated.AvailableLocales.Should().Contain("fr");

        Api.ClearAuthentication();

        var french = await ReadDataAsync<NoticeDto>(
            await HttpClient.GetAsync($"{Root}/notices/{notice}?locale=fr-MU"));

        french.Card.Title.Should().Be(
            "Nouvelles règles de visa", "fr-MU falls back to the fr translation");
        french.ResolvedLocale.Should().Be("fr");
    }

    [Fact]
    public async Task Superseding_names_the_replacement_and_keeps_the_original_readable()
    {
        await AuthenticateAsStaffAsync("Admin");

        var authority = await CreateVerifiedAuthorityAsync("MU", "Superseding Ministry");

        var original = await PublishedNoticeAsync("MU", authority);
        var replacement = await PublishedNoticeAsync("MU", authority);

        var superseded = await ReadDataAsync<NoticeDto>(
            await PostAsync(
                $"{Root}/notices/{original}/supersede",
                new { replacementNoticeId = replacement }));

        superseded.Card.Status.Should().Be("Superseded");
        superseded.Card.SupersededById.Should().Be(replacement);

        Api.ClearAuthentication();

        var readBack = await HttpClient.GetAsync($"{Root}/notices/{original}");

        readBack.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "somebody who followed the old rule needs to see what it said");
    }

    [Fact]
    public async Task Archiving_hides_a_notice_from_readers_and_restoring_returns_it_as_a_draft()
    {
        await AuthenticateAsStaffAsync("Admin");

        var authority = await CreateVerifiedAuthorityAsync("MU", "Archiving Ministry");
        var notice = await PublishedNoticeAsync("MU", authority);

        (await HttpClient.DeleteAsync($"{Root}/notices/{notice}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        Api.ClearAuthentication();

        (await HttpClient.GetAsync($"{Root}/notices/{notice}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await AuthenticateAsStaffAsync("Admin");

        // The read that sees past the global query filter. Without it a restore could only ever
        // answer "not found" — the blind spot the platform has recorded once and paid for once.
        var restored = await ReadDataAsync<NoticeDto>(
            await PostAsync($"{Root}/notices/{notice}/restore"));

        restored.Card.Status.Should().Be("Draft");
    }

    // ── Persistence of the owned collections ────────────────────────────────────────────────

    /// <summary>
    /// <b>The test that would have caught the mapping defect the architecture suite found first.</b>
    /// Attachments and references are owned collections keyed on caller-supplied identifiers, and
    /// without <c>ValueGeneratedNever</c> EF emits an UPDATE where an INSERT was meant — which
    /// surfaces as a bogus concurrency conflict on the <em>second</em> row, and never as anything
    /// that reads like a mapping fault.
    /// </summary>
    [Fact]
    public async Task Attachments_and_references_survive_a_round_trip_with_several_rows()
    {
        await AuthenticateAsStaffAsync("Admin");

        var authority = await CreateVerifiedAuthorityAsync("MU", "Attaching Ministry");
        var notice = await PublishableNoticeAsync("MU", authority);

        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var guide = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var saved = await ReadDataAsync<NoticeDto>(
            await PutAsync(
                $"{Root}/notices/{notice}",
                new
                {
                    attachments = new[]
                    {
                        new { mediaId = first, caption = "The circular" },
                        new { mediaId = second, caption = "The new form" }
                    },
                    references = new[]
                    {
                        new { kind = "Guide", targetId = guide, label = "How to apply" },
                        new { kind = "Event", targetId = eventId, label = "Outreach day" }
                    },
                    languages = new[] { "en", "fr" }
                }));

        saved.Attachments.Should().HaveCount(2);
        saved.References.Should().HaveCount(2);
        saved.Languages.Should().BeEquivalentTo(["en", "fr"]);

        Api.ClearAuthentication();

        // Read back through a fresh request, which is where "Collection is read-only" and the
        // phantom-UPDATE defect both surface.
        await AuthenticateAsStaffAsync("Admin");

        var readBack = await ReadDataAsync<NoticeDto>(
            await HttpClient.GetAsync($"{Root}/notices/{notice}"));

        readBack.Attachments.Select(attachment => attachment.MediaId)
            .Should().BeEquivalentTo([first, second]);

        readBack.References.Select(reference => reference.TargetId)
            .Should().BeEquivalentTo([guide, eventId]);
    }

    // ── Published contracts ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Publication fans out to two contracts for an emergency category and one for anything
    /// else.</b> A consumer subscribed to all official notices must not stop receiving the most
    /// important ones because they qualified for a narrower contract.
    /// </summary>
    [Fact]
    public async Task Publishing_an_emergency_notice_produces_both_contracts_through_the_outbox()
    {
        await QuietTheOutboxAsync();
        await AuthenticateAsStaffAsync("Admin");

        var authority = await CreateVerifiedAuthorityAsync("MU", "Disaster Management");

        var notice = await PublishedNoticeAsync(
            "MU", authority, category: "emergency-alerts", severity: "Critical");

        var messages = await DrainAndReadOutboxAsync();

        messages.Should().Contain(
            message => message.Contains(nameof(OfficialNoticePublishedIntegrationEvent)),
            "every published notice raises the general contract");

        messages.Should().Contain(
            message => message.Contains(nameof(EmergencyAlertPublishedIntegrationEvent)),
            "an emergency-family category raises a second one on top");

        // The summary is on the contract because a notification renders it. B13.1 records what
        // happens when a publisher writes a placeholder into a required field instead.
        messages.Where(message =>
                message.Contains(nameof(OfficialNoticePublishedIntegrationEvent)))
            .Should().Contain(
                message => message.Contains("The rules change on 1 April."),
                "an empty summary would reach every consumer, silently, forever");

        _ = notice;
    }

    [Fact]
    public async Task Publishing_a_government_announcement_produces_the_government_contract()
    {
        await QuietTheOutboxAsync();
        await AuthenticateAsStaffAsync("Admin");

        var authority = await CreateVerifiedAuthorityAsync("MU", "Prime Minister's Office");

        await PublishedNoticeAsync(
            "MU", authority, category: "government-announcements", severity: "Medium");

        var messages = await DrainAndReadOutboxAsync();

        messages.Should().Contain(
            message => message.Contains(nameof(GovernmentAnnouncementPublishedIntegrationEvent)));

        messages.Should().NotContain(
            message => message.Contains(nameof(EmergencyAlertPublishedIntegrationEvent)),
            "a government announcement is not an emergency unless its category says so");
    }

    // ── Emergency information ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Emergency_information_is_saved_as_a_page_and_read_by_anybody()
    {
        await AuthenticateAsStaffAsync("Moderator");

        var saved = await ReadDataAsync<EmergencyDto>(
            await PutAsync(
                $"{Root}/countries/KE/emergency",
                new
                {
                    guidance = "Dial 999 from a landline, 112 from a mobile.",
                    contacts = new[]
                    {
                        new
                        {
                            kind = "police",
                            label = "Police",
                            number = "999",
                            note = (string?)null,
                            isAvailableAllHours = true
                        },
                        new
                        {
                            kind = "ambulance",
                            label = "Ambulance",
                            number = "112",
                            note = (string?)"Nairobi and Mombasa only",
                            isAvailableAllHours = true
                        }
                    },
                    localizations = new[]
                    {
                        new { locale = "sw", kind = "police", label = "Polisi" }
                    },
                    markReviewed = true
                }));

        saved.Contacts.Should().HaveCount(2);
        saved.LastReviewedAt.Should().NotBeNull();

        Api.ClearAuthentication();

        var swahili = await ReadDataAsync<EmergencyDto>(
            await HttpClient.GetAsync($"{Root}/countries/KE/emergency?locale=sw"));

        swahili.Contacts.Single(contact => contact.Kind == "police").Label
            .Should().Be("Polisi");

        swahili.Contacts.Single(contact => contact.Kind == "ambulance").Label
            .Should().Be("Ambulance", "an untranslated label falls back to what the editor wrote");

        swahili.Guidance.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Two police numbers is a contradiction an editor must resolve, not one the platform should
    /// resolve by list order.
    /// </summary>
    [Fact]
    public async Task A_repeated_emergency_kind_is_refused()
    {
        await AuthenticateAsStaffAsync("Moderator");

        var response = await PutRawAsync(
            $"{Root}/countries/GH/emergency",
            new
            {
                contacts = new[]
                {
                    new { kind = "police", label = "Police", number = "191" },
                    new { kind = "police", label = "Police Service", number = "18555" }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await response.Content.ReadAsStringAsync())
            .Should().Contain("intelligence.emergency.duplicate-kind");
    }

    // ── The embassy directory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Where a mission is and whom it serves are different questions with almost no overlap, and the
    /// directory answers each without guessing at the other.
    /// </summary>
    [Fact]
    public async Task A_mission_is_found_by_where_it_is_and_by_whom_it_serves()
    {
        await AuthenticateAsStaffAsync("Moderator");

        var created = await ReadDataAsync<EmbassyDto>(
            await PostAsync(
                $"{Root}/embassies",
                new
                {
                    countryCode = "GB",
                    servesCountryCode = "NG",
                    name = "Nigerian High Commission London",
                    city = "London"
                }));

        created.CountryCode.Should().Be("GB");
        created.ServesCountryCode.Should().Be("NG");

        Api.ClearAuthentication();

        var located = await ReadDataAsync<PageDto<EmbassyDto>>(
            await HttpClient.GetAsync($"{Root}/embassies?locatedIn=GB"));

        located.Items.Should().Contain(
            embassy => embassy.EmbassyId == created.EmbassyId);

        var serving = await ReadDataAsync<PageDto<EmbassyDto>>(
            await HttpClient.GetAsync($"{Root}/embassies?serving=NG"));

        serving.Items.Should().Contain(
            embassy => embassy.EmbassyId == created.EmbassyId);

        var elsewhere = await ReadDataAsync<PageDto<EmbassyDto>>(
            await HttpClient.GetAsync($"{Root}/embassies?locatedIn=NG"));

        elsewhere.Items.Should().NotContain(
            embassy => embassy.EmbassyId == created.EmbassyId,
            "a mission in London is not a mission in Nigeria");
    }

    [Fact]
    public async Task A_mission_keeps_its_hours_services_and_emergency_line()
    {
        await AuthenticateAsStaffAsync("Moderator");

        var created = await ReadDataAsync<EmbassyDto>(
            await PostAsync(
                $"{Root}/embassies",
                new
                {
                    countryCode = "MU",
                    servesCountryCode = "IN",
                    name = "High Commission of India"
                }));

        var updated = await ReadDataAsync<EmbassyDto>(
            await PutAsync(
                $"{Root}/embassies/{created.EmbassyId}",
                new
                {
                    name = "High Commission of India",
                    city = "Port Louis",
                    telephone = "+230 208 3775",
                    emergencyTelephone = "+230 5250 0000",
                    offersPassportServices = true,
                    offersConsularServices = true,
                    requiresAppointment = true,
                    officeHours = new[]
                    {
                        new { day = "Monday", hours = "09:00-12:30", isClosed = false },
                        new { day = "Sunday", hours = "closed", isClosed = true }
                    },
                    services = new[]
                    {
                        new
                        {
                            key = "police-clearance",
                            name = "Police Clearance Certificate",
                            requiresAppointment = true
                        }
                    }
                }));

        updated.EmergencyTelephone.Should().Be("+230 5250 0000");
        updated.OffersPassportServices.Should().BeTrue();
        updated.OfficeHours.Should().HaveCount(2);
        updated.OfficeHours.Single(row => row.Day == "Sunday").IsClosed.Should().BeTrue();
        updated.Services.Should().ContainSingle();
    }

    // ── Visa advisories ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The most dangerous kind of wrong this module could be.</b> A listing that matched only the
    /// exact nationality would tell most members that no rules exist for them.
    /// </summary>
    [Fact]
    public async Task Asking_for_a_nationality_also_returns_the_rules_that_apply_to_everybody()
    {
        await AuthenticateAsStaffAsync("Admin");

        var authority = await CreateVerifiedAuthorityAsync("RW", "Directorate of Immigration");

        var general = await CreateAdvisoryAsync("RW", appliesTo: null, authority, "Visa on arrival");
        var specific = await CreateAdvisoryAsync("RW", "NG", authority, "Nigerian nationals");
        var other = await CreateAdvisoryAsync("RW", "FR", authority, "French nationals");

        foreach (var advisory in new[] { general, specific, other })
        {
            await PostAsync($"{Root}/visas/{advisory}/publish");
        }

        Api.ClearAuthentication();

        var page = await ReadDataAsync<PageDto<VisaDto>>(
            await HttpClient.GetAsync($"{Root}/countries/RW/visas?appliesTo=NG"));

        var ids = page.Items.Select(item => item.VisaAdvisoryId).ToList();

        ids.Should().Contain(general, "a general rule applies to a Nigerian passport holder too");
        ids.Should().Contain(specific);
        ids.Should().NotContain(other, "French rules do not apply to a Nigerian passport holder");
    }

    // ── Public holidays ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_calendar_answers_a_year_and_a_range()
    {
        await AuthenticateAsStaffAsync("Moderator");

        await PostAsync(
            $"{Root}/holidays",
            new
            {
                countryCode = "MU",
                name = "Independence Day",
                kind = "National",
                observedOn = "2026-03-12T00:00:00Z",
                closesGovernmentOffices = true,
                closesEmbassies = true
            });

        await PostAsync(
            $"{Root}/holidays",
            new
            {
                countryCode = "MU",
                name = "Thaipusam Cavadee",
                kind = "Religious",
                observedOn = "2026-02-01T00:00:00Z",
                closesGovernmentOffices = true,
                closesEmbassies = false
            });

        Api.ClearAuthentication();

        var year = await ReadDataAsync<IReadOnlyList<HolidayDto>>(
            await HttpClient.GetAsync($"{Root}/countries/MU/holidays?year=2026"));

        year.Should().HaveCountGreaterThanOrEqualTo(2);

        year.Select(holiday => holiday.ObservedOn).Should().BeInAscendingOrder();

        year.Single(holiday => holiday.Name == "Thaipusam Cavadee")
            .ClosesEmbassies.Should().BeFalse(
                "a religious holiday closing government offices need not close missions");

        var range = await ReadDataAsync<IReadOnlyList<HolidayDto>>(
            await HttpClient.GetAsync(
                $"{Root}/countries/MU/holidays?from=2026-03-01T00:00:00Z&until=2026-03-31T00:00:00Z"));

        range.Should().OnlyContain(holiday => holiday.Name == "Independence Day");
    }

    // ── The country dashboard ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The brief asks for two things that look like they conflict:</b> every country dashboard
    /// surfaces official information, and country dashboards must not be duplicated. One additive
    /// property on B8.1's existing response resolves both.
    /// </summary>
    [Fact]
    public async Task The_country_knowledge_dashboard_gains_an_intelligence_band()
    {
        await AuthenticateAsStaffAsync("Admin");

        await EnsurePublishedDirectoryAsync("ZA");

        var authority = await CreateVerifiedAuthorityAsync("ZA", "Department of Home Affairs");

        await PublishedNoticeAsync("ZA", authority, severity: "Critical");

        await PutAsync(
            $"{Root}/countries/ZA/emergency",
            new
            {
                contacts = new[]
                {
                    new { kind = "police", label = "SAPS", number = "10111", isAvailableAllHours = true }
                }
            });

        await PostAsync(
            $"{Root}/embassies",
            new
            {
                countryCode = "ZA",
                servesCountryCode = "ZW",
                name = "Embassy of Zimbabwe, Pretoria"
            });

        Api.ClearAuthentication();

        var dashboard = await ReadDataAsync<DashboardDto>(
            await HttpClient.GetAsync($"{KnowledgeRoot}/countries/ZA/dashboard"));

        dashboard.Intelligence.Should().NotBeNull(
            "official information reaches the dashboard as a band, not as a second dashboard");

        dashboard.Intelligence!.ActiveNoticeCount.Should().BeGreaterThan(0);
        dashboard.Intelligence.UrgentNoticeCount.Should().BeGreaterThan(0);
        dashboard.Intelligence.Notices.Should().NotBeEmpty();

        dashboard.Intelligence.EmergencyContacts.Should().ContainSingle(
            "the numbers are on the band itself — a member in an emergency has one tap");

        dashboard.Intelligence.Route.Should().Be($"{Root}/countries/ZA");

        // And the count bands, contributed through B8.1's own seam alongside jobs and housing. The
        // registry drops a zero-count block, so both counters have to be non-zero to appear — which
        // is why this test creates a mission as well as a notice.
        dashboard.Context.Should().Contain(block => block.Kind == "official-notices");
        dashboard.Context.Should().Contain(block => block.Kind == "embassies");
    }

    [Fact]
    public async Task The_intelligence_page_composes_every_band_the_brief_names()
    {
        await AuthenticateAsStaffAsync("Admin");

        var authority = await CreateVerifiedAuthorityAsync("TZ", "Immigration Services");

        await PublishedNoticeAsync(
            "TZ", authority, category: "visa-advisories", severity: "Medium");
        await PublishedNoticeAsync(
            "TZ", authority, category: "emergency-alerts", severity: "Critical");
        await PublishedNoticeAsync(
            "TZ", authority, category: "government-announcements", severity: "Medium");

        Api.ClearAuthentication();

        var page = await ReadDataAsync<CountryIntelligenceDto>(
            await HttpClient.GetAsync($"{Root}/countries/TZ"));

        page.CountryCode.Should().Be("TZ");
        page.LatestNotices.Should().NotBeEmpty();
        page.SafetyAlerts.Should().NotBeEmpty();
        page.VisaUpdates.Should().NotBeEmpty();
        page.GovernmentAdvisories.Should().NotBeEmpty();
        page.OfficialContacts.Should().NotBeEmpty();
        page.CategoryBreakdown.Should().NotBeEmpty();

        page.Statistics.ActiveNoticeCount.Should().Be(3);
        page.Statistics.UrgentNoticeCount.Should().Be(1);
        page.Statistics.VerifiedAuthorityCount.Should().BeGreaterThan(0);
        page.Statistics.LastPublishedAt.Should().NotBeNull();

        // The briefing order: severity first, not recency. The critical alert was published second
        // of three and must still lead.
        page.LatestNotices[0].Severity.Should().Be("Critical");
    }

    // ── Search ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_answers_from_the_write_model_and_reports_which_engine_did()
    {
        await AuthenticateAsStaffAsync("Admin");

        var authority = await CreateVerifiedAuthorityAsync("UG", "Directorate of Citizenship");

        await PublishedNoticeAsync(
            "UG", authority, title: "Passport appointment backlog cleared");

        Api.ClearAuthentication();

        var found = await ReadDataAsync<SearchDto>(
            await HttpClient.GetAsync($"{Root}/search?q=backlog&countryCode=UG"));

        found.Engine.Should().Be("ef");
        found.Results.Should().Contain(
            result => result.Title.Contains("backlog", StringComparison.Ordinal));

        var empty = await ReadDataAsync<SearchDto>(
            await HttpClient.GetAsync($"{Root}/search?q=nothingmatchesthis&countryCode=UG"));

        empty.Results.Should().BeEmpty();
        empty.Query.Should().Be("nothingmatchesthis");
    }

    // ── Analytics ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Recording_activity_moves_the_notice_and_the_country_counters()
    {
        await AuthenticateAsStaffAsync("Admin");

        var authority = await CreateVerifiedAuthorityAsync("ZM", "Department of Immigration");
        var notice = await PublishedNoticeAsync("ZM", authority);

        Api.ClearAuthentication();

        await PostAsync(
            $"{Root}/notices/{notice}/activity",
            new { activity = "view", source = "notification" });

        await PostAsync(
            $"{Root}/notices/{notice}/activity", new { activity = "click-through" });

        await AuthenticateAsStaffAsync("Moderator");

        var analytics = await ReadDataAsync<AnalyticsDto>(
            await HttpClient.GetAsync($"{Root}/countries/ZM/analytics"));

        analytics.NoticeViews.Should().Be(1);
        analytics.ClickThroughs.Should().Be(1);
        analytics.NotificationsFollowed.Should().Be(
            1, "the delivery correlation costs one counter and no knowledge of what B13 is");
        analytics.ClickThroughRate.Should().Be(100);
        analytics.MostViewed.Should().NotBeEmpty();
    }

    /// <summary>
    /// A rate with a zero denominator is null rather than zero: "nobody clicked through on zero
    /// views" and "nobody clicked through on ten thousand" are opposite findings.
    /// </summary>
    [Fact]
    public async Task A_country_with_no_activity_reports_null_rates_rather_than_zero()
    {
        await AuthenticateAsStaffAsync("Moderator");

        var analytics = await ReadDataAsync<AnalyticsDto>(
            await HttpClient.GetAsync($"{Root}/countries/BW/analytics"));

        analytics.NoticeViews.Should().Be(0);
        analytics.ClickThroughRate.Should().BeNull();
        analytics.ReadCompletionRate.Should().BeNull();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<AuthorityDto> CreateAuthorityAsync(string countryCode, string name) =>
        await ReadDataAsync<AuthorityDto>(
            await PostAsync(
                $"{Root}/authorities",
                new
                {
                    countryCode,
                    name,
                    authorityType = "ImmigrationDepartment",
                    website = "https://immigration.example.gov"
                }));

    /// <summary>
    /// An authority with the badge on.
    /// <para>
    /// Verification is Trust's decision and arrives as a published event, so a test that needed the
    /// whole round trip would be testing B3. This sets the projection directly through the aggregate
    /// — the same call the inbound projector makes — which is what leaves the publication gate itself
    /// genuinely under test rather than stubbed.
    /// </para>
    /// </summary>
    private async Task<Guid> CreateVerifiedAuthorityAsync(string countryCode, string name)
    {
        var authority = await CreateAuthorityAsync(countryCode, name);

        using var scope = Factory.Services.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IIssuingAuthorityRepository>();
        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<Diaspora.Domain.Abstractions.IUnitOfWork>();

        var loaded = await repository.GetAsync(
            IssuingAuthorityId.From(authority.IssuingAuthorityId));

        loaded.Should().NotBeNull();

        loaded!.ApplyVerification(verified: true, DateTimeOffset.UtcNow);

        await unitOfWork.SaveChangesAsync();

        return authority.IssuingAuthorityId;
    }

    private async Task<NoticeDto> CreateNoticeAsync(
        string countryCode,
        Guid authorityId,
        string category = "visa-advisories",
        string severity = "High",
        string title = "New visa rules") =>
        await ReadDataAsync<NoticeDto>(
            await PostAsync(
                $"{Root}/notices",
                new
                {
                    countryCode,
                    categoryKey = category,
                    authorityId,
                    title,
                    summary = "The rules change on 1 April.",
                    content = "Full text of the circular.",
                    severity
                }));

    /// <summary>A draft with a cited source, so publication is refused only by the gates under test.</summary>
    private async Task<Guid> PublishableNoticeAsync(
        string countryCode,
        Guid authorityId,
        string category = "visa-advisories",
        string severity = "High",
        string title = "New visa rules")
    {
        var notice = await CreateNoticeAsync(countryCode, authorityId, category, severity, title);

        await PutAsync(
            $"{Root}/notices/{notice.Card.OfficialNoticeId}",
            new
            {
                referenceNumber = "MHA/IMM/2026/014",
                sourceUrl = "https://immigration.example.gov/circulars/14"
            });

        return notice.Card.OfficialNoticeId;
    }

    private async Task<Guid> PublishedNoticeAsync(
        string countryCode,
        Guid authorityId,
        string category = "visa-advisories",
        string severity = "High",
        string title = "New visa rules")
    {
        var notice = await PublishableNoticeAsync(
            countryCode, authorityId, category, severity, title);

        await PostAsync($"{Root}/notices/{notice}/submit");
        await VerifyAsync(notice, approved: true);
        await PostAsync($"{Root}/notices/{notice}/publish");

        return notice;
    }

    private async Task<NoticeDto> VerifyAsync(Guid noticeId, bool approved) =>
        await ReadDataAsync<NoticeDto>(
            await PostAsync(
                $"{Root}/notices/{noticeId}/verify",
                new { approved, note = approved ? "Checked against the gazette." : "No source." }));

    private async Task<Guid> CreateAdvisoryAsync(
        string countryCode, string? appliesTo, Guid authorityId, string title)
    {
        var advisory = await ReadDataAsync<VisaDto>(
            await PostAsync(
                $"{Root}/visas",
                new
                {
                    countryCode,
                    appliesToCountryCode = appliesTo,
                    authorityId,
                    title,
                    summary = "Entry rules."
                }));

        return advisory.VisaAdvisoryId;
    }

    /// <summary>
    /// A published knowledge directory, which the dashboard read requires. Created idempotently so
    /// the test does not depend on which order the suite ran in.
    /// </summary>
    private async Task EnsurePublishedDirectoryAsync(string countryCode)
    {
        var created = await HttpClient.PostAsJsonAsync(
            $"{KnowledgeRoot}/admin/countries/{countryCode}",
            new { displayName = countryCode },
            JsonOptions);

        created.StatusCode.Should().BeOneOf(
            [HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.Conflict],
            "the directory is either opened here or was opened by an earlier test");

        var published = await HttpClient.PostAsync(
            $"{KnowledgeRoot}/admin/countries/{countryCode}/publish", content: null);

        published.StatusCode.Should().BeOneOf(
            [HttpStatusCode.OK, HttpStatusCode.Conflict],
            "already published is the same end state");
    }

    /// <summary>
    /// Everything the outbox is holding, as "type + payload" lines.
    /// <para>
    /// Read <em>before</em> draining, because a delivered message is gone: the queue is where a
    /// publication leaves its durable promise, and asserting on that promise is what proves the
    /// relay went through B1.1 rather than calling a consumer in process.
    /// </para>
    /// </summary>
    /// <summary>
    /// Empties the queue so what is pending after the act under test is that act's own doing.
    /// <para>
    /// Without this, the seeded catalogue's several hundred messages sit ahead of the one being
    /// asserted on and the pending read — which is oldest-first and bounded — never reaches it. The
    /// symptom is a test that passes early in a run and fails later in it.
    /// </para>
    /// </summary>
    private async Task QuietTheOutboxAsync() => await Factory.DrainOutboxAsync();

    private async Task<IReadOnlyList<string>> DrainAndReadOutboxAsync()
    {
        var pending = await Factory.PendingOutboxAsync();

        var messages = pending
            .Select(message => $"{message.EventType}|{message.Payload}")
            .ToList();

        await Factory.DrainOutboxAsync();

        return messages;
    }

    private Task<HttpResponseMessage> PostAsync(string route, object? body = null) =>
        body is null
            ? HttpClient.PostAsync(route, content: null)
            : HttpClient.PostAsJsonAsync(route, body, JsonOptions);

    private Task<HttpResponseMessage> PutAsync(string route, object body) =>
        HttpClient.PutAsJsonAsync(route, body, JsonOptions);

    private Task<HttpResponseMessage> PutRawAsync(string route, object body) =>
        HttpClient.PutAsJsonAsync(route, body, JsonOptions);

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(
            "the request failed: {0}", await response.Content.ReadAsStringAsync());

        return await IdentityApiClient.ReadDataAsync<T>(response);
    }

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

    // ── Wire shapes, declared by hand ───────────────────────────────────────────────────────

    private sealed record PageDto<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);

    private sealed record CategoryDto(
        Guid NoticeCategoryId,
        string Key,
        string Name,
        string DefaultSeverity,
        string AlertFamily,
        bool IsActive);

    private sealed record EmergencyKindDto(string Key, string Name, bool IsEssential);

    private sealed record CapabilitiesDto(
        IReadOnlyList<CategoryDto> Categories,
        IReadOnlyList<EmergencyKindDto> EmergencyKinds,
        IReadOnlyList<string> AuthorityTypes,
        IReadOnlyList<string> Severities,
        IReadOnlyList<string> SortOptions,
        string SearchEngine,
        bool SearchAvailable,
        bool AuthorityVerificationAvailable,
        IReadOnlyList<string> SourceValidators,
        IReadOnlyList<string> AnalyticsSinks,
        int HolidayHorizonDays);

    private sealed record AuthorityDto(
        Guid IssuingAuthorityId,
        string CountryCode,
        string Name,
        string AuthorityType,
        bool IsVerified);

    private sealed record NoticeCardDto(
        Guid OfficialNoticeId,
        string Title,
        string Summary,
        string CountryCode,
        string CategoryKey,
        string CategoryName,
        string Severity,
        string Status,
        string VerificationStatus,
        Guid AuthorityId,
        string AuthorityName,
        bool IsOfficial,
        bool IsActive,
        bool IsExpired,
        Guid? SupersededById,
        long ViewCount);

    private sealed record AttachmentDto(Guid MediaId, string? Caption, int Position);

    private sealed record ReferenceDto(string Kind, Guid TargetId, string? Label, int Position);

    private sealed record NoticeDto(
        NoticeCardDto Card,
        string Content,
        string? ReferenceNumber,
        string? SourceUrl,
        IReadOnlyList<string> Languages,
        IReadOnlyList<string> AvailableLocales,
        string ResolvedLocale,
        IReadOnlyList<AttachmentDto> Attachments,
        IReadOnlyList<ReferenceDto> References,
        int? Revision);

    private sealed record EmergencyContactDto(
        string Kind, string Label, string Number, string? Note, bool IsAvailableAllHours);

    private sealed record EmergencyDto(
        string CountryCode,
        string? Guidance,
        IReadOnlyList<EmergencyContactDto> Contacts,
        DateTimeOffset? LastReviewedAt,
        int? Revision);

    private sealed record OfficeHoursDto(string Day, string? Hours, bool IsClosed);

    private sealed record EmbassyServiceDto(string Key, string Name, bool RequiresAppointment);

    private sealed record EmbassyDto(
        Guid EmbassyId,
        string Name,
        string CountryCode,
        string ServesCountryCode,
        string? City,
        string? Telephone,
        string? EmergencyTelephone,
        bool OffersPassportServices,
        bool OffersVisaServices,
        bool OffersConsularServices,
        bool OffersEmergencyAssistance,
        bool IsVerified,
        IReadOnlyList<OfficeHoursDto> OfficeHours,
        IReadOnlyList<EmbassyServiceDto> Services,
        int? Revision);

    private sealed record PlatformCardDto(
        Guid OfficialNoticeId,
        string Status,
        string AuthorityName,
        bool IsOfficial,
        bool PostedByPlatform);

    private sealed record PlatformNoticeDto(PlatformCardDto Card);

    private sealed record PlatformVisaDto(
        Guid VisaAdvisoryId, string Status, bool IsOfficial, bool PostedByPlatform);

    private sealed record VisaDto(
        Guid VisaAdvisoryId,
        string CountryCode,
        string? AppliesToCountryCode,
        string Title,
        string Status,
        Guid AuthorityId,
        IReadOnlyList<string> Requirements,
        IReadOnlyList<string> Documents,
        bool IsActive,
        int? Revision);

    private sealed record HolidayDto(
        Guid PublicHolidayId,
        string CountryCode,
        string Name,
        string Kind,
        DateTimeOffset ObservedOn,
        DateTimeOffset ObservedUntil,
        int Year,
        bool ClosesGovernmentOffices,
        bool ClosesEmbassies);

    private sealed record StatisticsDto(
        int ActiveNoticeCount,
        int UrgentNoticeCount,
        int EmbassyCount,
        int VisaAdvisoryCount,
        int EmergencyContactCount,
        int UpcomingHolidayCount,
        int VerifiedAuthorityCount,
        DateTimeOffset? LastPublishedAt);

    private sealed record CategoryCountDto(string Key, string Name, string? Icon, int Count);

    private sealed record CountryIntelligenceDto(
        string CountryCode,
        StatisticsDto Statistics,
        IReadOnlyList<NoticeCardDto> LatestNotices,
        IReadOnlyList<NoticeCardDto> SafetyAlerts,
        IReadOnlyList<NoticeCardDto> VisaUpdates,
        IReadOnlyList<NoticeCardDto> GovernmentAdvisories,
        IReadOnlyList<EmbassyDto> Embassies,
        EmergencyDto? Emergency,
        IReadOnlyList<VisaDto> ConsularServices,
        IReadOnlyList<HolidayDto> UpcomingHolidays,
        IReadOnlyList<AuthorityDto> OfficialContacts,
        IReadOnlyList<CategoryCountDto> CategoryBreakdown);

    private sealed record IntelligenceSummaryDto(
        int ActiveNoticeCount,
        int UrgentNoticeCount,
        IReadOnlyList<NoticeCardDto> Notices,
        IReadOnlyList<EmergencyContactDto> EmergencyContacts,
        int EmbassyCount,
        IReadOnlyList<HolidayDto> UpcomingHolidays,
        DateTimeOffset? LastPublishedAt,
        string Route);

    private sealed record ContextBlockDto(string Kind, string Label, int Count, string? Route);

    private sealed record DashboardDto(
        string CountryCode,
        IReadOnlyList<ContextBlockDto> Context,
        IntelligenceSummaryDto? Intelligence);

    private sealed record SearchDto(
        IReadOnlyList<NoticeCardDto> Results,
        bool HasMore,
        string Engine,
        string? Query);

    private sealed record AnalyticsDto(
        string CountryCode,
        long NoticeViews,
        long ClickThroughs,
        long ReadCompletions,
        long DashboardViews,
        long EmergencyViews,
        long NotificationsFollowed,
        long RecommendationsAccepted,
        double? ClickThroughRate,
        double? ReadCompletionRate,
        IReadOnlyList<NoticeCardDto> MostViewed);
}
