using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B13.1 end to end: a real request over HTTP, a real outbox row, and a recipient who could not
/// have been named before this phase.
/// <para>
/// <b>Every assertion here reads the outbox payload rather than a mock.</b> That is the only place
/// the claim can honestly be checked — an enrichment is a promise about what crosses a module
/// boundary, and a test that inspected the publishing module's own objects would be checking that
/// the code does what the code does. What is asserted is the JSON: the field is present, it names
/// the right person, and it got there without any downstream module asking upstream a question.
/// </para>
/// <para>
/// <b>The drain is explicit.</b> The background dispatcher is off in this host, so a test that
/// wants delivery performs its action, drains, and then looks. Nothing here waits on a timer.
/// </para>
/// </summary>
public sealed class EventEnrichmentFlowTests : IdentityTestBase
{
    public EventEnrichmentFlowTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Jobs → Communication ────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>An application now names somebody at the employer.</b> Before B13.1 the payload carried
    /// the applicant and no other person, so "you have a new application" was a message the
    /// platform could describe and could not send.
    /// </summary>
    [Fact]
    public async Task An_application_carries_the_employer_member_who_should_be_told()
    {
        Factory.EnsureDatabaseCreated();

        var employer = await PublishAJobAsync();

        await ApplyAsync(employer.JobId);

        var payload = await PendingPayloadAsync(
            "Diaspora.Contracts.Jobs.V1.Events.ApplicationSubmittedIntegrationEvent");

        payload.Should().NotBeNull("the application must have reached the outbox");

        payload!["EmployerUserId"]!.GetValue<Guid>()
            .Should().Be(
                employer.OwnerUserId,
                "the employer's own member is on the contract, so Communication never has to ask " +
                "Jobs who runs the organisation");

        payload["ApplicantUserId"]!.GetValue<Guid>()
            .Should().NotBe(Guid.Empty, "and the candidate side is unchanged");
    }

    /// <summary>
    /// <b>The defect B13.1 fixed, observed on the wire.</b> Shortlisting declared a required
    /// candidate member and wrote <see cref="Guid.Empty"/> into it, because the relay had no route
    /// from a hiring decision to the applying member. The good news went to an identifier nobody
    /// holds, and nothing failed.
    /// </summary>
    [Fact]
    public async Task A_shortlisting_names_a_real_member_rather_than_the_empty_identifier()
    {
        Factory.EnsureDatabaseCreated();

        var employer = await PublishAJobAsync();
        var applicant = await ApplyAsync(employer.JobId);

        await ShortlistAsync(employer.OwnerToken, applicant.ApplicationId);

        var payload = await PendingPayloadAsync(
            "Diaspora.Contracts.Jobs.V1.Events.CandidateShortlistedIntegrationEvent");

        payload.Should().NotBeNull("shortlisting publishes its own contract");

        payload!["ApplicantUserId"]!.GetValue<Guid>()
            .Should().Be(
                applicant.UserId,
                "the shortlisting must reach the person who was shortlisted, and for three " +
                "contracts it reached nobody");

        payload["EmployerUserId"]!.GetValue<Guid>().Should().Be(employer.OwnerUserId);
    }

    /// <summary>
    /// The same fact one step further on: Communication turns the enriched payload into an inbox
    /// row, resolving the recipient from the contract alone.
    /// </summary>
    [Fact]
    public async Task A_shortlisting_reaches_the_candidates_inbox()
    {
        Factory.EnsureDatabaseCreated();

        var employer = await PublishAJobAsync();
        var applicant = await ApplyAsync(employer.JobId);

        await ShortlistAsync(employer.OwnerToken, applicant.ApplicationId);

        await Factory.DrainOutboxAsync();

        Api.Authenticate(applicant.Token);

        (await UnreadCountAsync()).Should().BeGreaterThan(
            0,
            "the Communication Platform addressed the candidate from the published contract, " +
            "without querying Jobs for anything");
    }

    // ── Community Life → Communication ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>An RSVP now names the organizer.</b> The gap B13 recorded in its own consumer: the module
    /// could confirm a response to the person who made it and could not tell the person running
    /// the hall.
    /// </summary>
    [Fact]
    public async Task An_rsvp_carries_the_organizer()
    {
        Factory.EnsureDatabaseCreated();

        var published = await PublishAnEventAsync();

        var (_, attendee) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(attendee.Tokens.AccessToken);

        var responded = await HttpClient.PutAsJsonAsync(
            $"/api/v1/community-life/events/{published.EventId}/rsvp",
            new { status = "Going", partySize = 2 });

        responded.StatusCode.Should().BeOneOf(
            [HttpStatusCode.OK, HttpStatusCode.Created],
            await responded.Content.ReadAsStringAsync());

        var payload = await PendingPayloadAsync(
            "Diaspora.Contracts.CommunityLife.V1.Events.RsvpSubmittedIntegrationEvent");

        payload.Should().NotBeNull("a commitment must have reached the outbox");

        payload!["OrganizerUserId"]!.GetValue<Guid>().Should().Be(published.OrganizerUserId);

        payload["AttendeeUserId"]!.GetValue<Guid>()
            .Should().Be(attendee.User.Id)
            .And.NotBe(published.OrganizerUserId, "the two sides are different people");
    }

    // ── Community Life → Communication, B13.3 ───────────────────────────────────────────────

    /// <summary>
    /// <b>The whole runtime, end to end, for the fact the frontend's Phase 12 register said nobody
    /// was being told.</b>
    /// <para>
    /// A member says they are coming, the organizer calls the event off, and the member's inbox says
    /// so. Between those two sentences: a domain event, an outbox row, a published contract, a B13
    /// consumer, an audience resolved through the kernel's channel seam against Community Life's own
    /// RSVP table, a rule, a template and a notification. Every one of those could be individually
    /// correct and the chain still not work — a rule naming an unpublished template, a consumer
    /// registered for the wrong contract name, an audience seam nobody claimed — and each of those
    /// failures is silent. This is the assertion that they are all connected.
    /// </para>
    /// <para>
    /// <b>Asserted on the attendee's inbox, not on the organizer's.</b> The organizer knows; they
    /// pressed the button. The point of the phase is the person who had rearranged their evening.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_cancelled_event_reaches_the_inbox_of_everybody_who_was_going()
    {
        Factory.EnsureDatabaseCreated();

        var published = await PublishAnEventAsync();

        var (_, attendee) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(attendee.Tokens.AccessToken);

        (await HttpClient.PutAsJsonAsync(
                $"/api/v1/community-life/events/{published.EventId}/rsvp",
                new { status = "Going", partySize = 1 }))
            .StatusCode.Should().BeOneOf([HttpStatusCode.OK, HttpStatusCode.Created]);

        // Drained before the cancellation so the RSVP's own confirmation is already delivered and
        // the count below can only have moved because of the cancellation.
        await Factory.DrainOutboxAsync();

        Api.Authenticate(attendee.Tokens.AccessToken);
        var before = await UnreadCountAsync();

        Api.Authenticate(published.OrganizerToken);

        var cancelled = await HttpClient.PostAsJsonAsync(
            $"/api/v1/community-life/events/{published.EventId}/cancel",
            new { reason = "The hall is unavailable." });

        cancelled.StatusCode.Should().Be(
            HttpStatusCode.OK, await cancelled.Content.ReadAsStringAsync());

        await Factory.DrainOutboxAsync();

        Api.Authenticate(attendee.Tokens.AccessToken);

        (await UnreadCountAsync()).Should().BeGreaterThan(
            before,
            "the attendee list is resolved through the channel seam against Community Life's own "
            + "RSVP table, so a cancellation reaches the people it inconveniences without the "
            + "Communication Platform ever learning what an RSVP is");
    }

    /// <summary>
    /// <b>The enrichment the reminder could not exist without.</b> A scheduled rule computes its
    /// offset against the moment the event names, and this contract named none — so the reminder rule
    /// would have resolved to "no subject moment" and, by the dispatcher's documented behaviour, sent
    /// immediately. A member would have been reminded about an event the instant they said they were
    /// coming.
    /// </summary>
    [Fact]
    public async Task An_rsvp_carries_the_moment_a_reminder_is_scheduled_against()
    {
        Factory.EnsureDatabaseCreated();

        var published = await PublishAnEventAsync();

        var (_, attendee) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(attendee.Tokens.AccessToken);

        await HttpClient.PutAsJsonAsync(
            $"/api/v1/community-life/events/{published.EventId}/rsvp",
            new { status = "Going", partySize = 1 });

        var payload = await PendingPayloadAsync(
            "Diaspora.Contracts.CommunityLife.V1.Events.RsvpSubmittedIntegrationEvent");

        payload.Should().NotBeNull();

        payload!["EventStartsAt"]!.GetValue<DateTimeOffset>()
            .Should().BeAfter(DateTimeOffset.UtcNow, "the event has not happened yet");

        payload["EventTitle"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace(
            "a reminder that could not name the event would be worse than none for a member with "
            + "three responses in a week");

        payload["OrganizerUserId"]!.GetValue<Guid>().Should().Be(
            published.OrganizerUserId, "and B13.1's enrichment is unchanged beside it");
    }

    // ── Providers → Communication ───────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A review now names the business's owner.</b> The client's original "New review from
    /// Amara K." was the first item in its own mock notification list, and it was undeliverable
    /// until this field existed.
    /// </summary>
    [Fact]
    public async Task A_review_carries_the_provider_owner()
    {
        Factory.EnsureDatabaseCreated();

        var provider = await PublishAProviderAsync();

        var (_, reviewer) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reviewer.Tokens.AccessToken);

        var reviewed = await HttpClient.PostAsJsonAsync(
            $"/api/v1/providers/{provider.ProviderId}/reviews",
            new { rating = 5, title = "Excellent", body = "Prompt, fair and careful work." });

        reviewed.StatusCode.Should().BeOneOf(
            [HttpStatusCode.OK, HttpStatusCode.Created],
            await reviewed.Content.ReadAsStringAsync());

        var payload = await PendingPayloadAsync(
            "Diaspora.Contracts.Providers.V1.Events.ProviderReviewReceivedIntegrationEvent");

        payload.Should().NotBeNull();

        payload!["OwnerUserId"]!.GetValue<Guid>()
            .Should().Be(
                provider.OwnerUserId,
                "the provider's owner travels on the contract so Communication never asks B7 who " +
                "runs the business");

        payload["ReviewerId"]!.GetValue<Guid>()
            .Should().Be(reviewer.User.Id, "and the reviewer is still named, unchanged");
    }

    /// <summary>
    /// The owner's message arrives. Two triggers come out of one review — a receipt to the
    /// reviewer and news for the owner — and only the second is new.
    /// </summary>
    [Fact]
    public async Task A_review_reaches_the_provider_owners_inbox()
    {
        Factory.EnsureDatabaseCreated();

        var provider = await PublishAProviderAsync();

        var (_, reviewer) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(reviewer.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            $"/api/v1/providers/{provider.ProviderId}/reviews",
            new { rating = 5, title = "Excellent", body = "Prompt, fair and careful work." });

        await Factory.DrainOutboxAsync();

        Api.Authenticate(provider.OwnerToken);

        (await UnreadCountAsync()).Should().BeGreaterThan(
            0, "the owner is addressable from the payload for the first time");
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────

    private sealed record PublishedJob(Guid JobId, string OwnerToken, Guid OwnerUserId);

    private sealed record Applicant(Guid ApplicationId, string Token, Guid UserId);

    private sealed record PublishedEvent(Guid EventId, Guid OrganizerUserId, string OrganizerToken);

    private sealed record PublishedProvider(Guid ProviderId, string OwnerToken, Guid OwnerUserId);

    private async Task<PublishedJob> PublishAJobAsync()
    {
        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var employerId = await IdOfAsync(
            await HttpClient.PostAsJsonAsync(
                "/api/v1/jobs/employers",
                new
                {
                    displayName = $"Enrichment Employer {Guid.NewGuid():N}",
                    organizationType = "Company",
                    countryCode = "DE"
                }),
            "employerId");

        var jobId = await IdOfAsync(
            await HttpClient.PostAsJsonAsync(
                "/api/v1/jobs/",
                new
                {
                    employerId,
                    title = "Ward Nurse",
                    description = "A ward nursing role, described at enough length to be valid.",
                    categoryKey = "healthcare",
                    jobType = "FullTime",
                    experienceLevel = "Mid",
                    countryCode = "DE"
                }),
            "jobId");

        var published = await HttpClient.PostAsJsonAsync(
            $"/api/v1/jobs/{jobId}/publish", new { openImmediately = true });

        published.StatusCode.Should().Be(
            HttpStatusCode.OK, await published.Content.ReadAsStringAsync());

        return new PublishedJob(jobId, owner.Tokens.AccessToken, owner.User.Id);
    }

    private async Task<Applicant> ApplyAsync(Guid jobId)
    {
        var (_, candidate) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(candidate.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            "/api/v1/jobs/candidate/profile",
            new { headline = "Registered nurse, eight years", countryCode = "NG" });

        var applicationId = await IdOfAsync(
            await HttpClient.PostAsJsonAsync(
                $"/api/v1/jobs/{jobId}/apply", new { coverLetter = (string?)null }),
            "applicationId");

        return new Applicant(applicationId, candidate.Tokens.AccessToken, candidate.User.Id);
    }

    /// <summary>Walks an application to <c>Shortlisted</c>, which is a legal two-step move.</summary>
    private async Task ShortlistAsync(string employerToken, Guid applicationId)
    {
        Api.Authenticate(employerToken);

        var reviewed = await HttpClient.PostAsJsonAsync(
            $"/api/v1/jobs/applications/{applicationId}/decide", new { status = "UnderReview" });

        reviewed.StatusCode.Should().Be(
            HttpStatusCode.OK, await reviewed.Content.ReadAsStringAsync());

        var shortlisted = await HttpClient.PostAsJsonAsync(
            $"/api/v1/jobs/applications/{applicationId}/decide", new { status = "Shortlisted" });

        shortlisted.StatusCode.Should().Be(
            HttpStatusCode.OK, await shortlisted.Content.ReadAsStringAsync());
    }

    private async Task<PublishedEvent> PublishAnEventAsync()
    {
        var (_, organizer) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(organizer.Tokens.AccessToken);

        var eventId = await IdOfAsync(
            await HttpClient.PostAsJsonAsync(
                "/api/v1/community-life/events",
                new
                {
                    title = $"Independence Day Gathering {Guid.NewGuid():N}",
                    summary = "An evening of food, music and conversation for the community.",
                    categoryKey = "cultural-celebration",
                    startsAt = DateTimeOffset.UtcNow.AddDays(14),
                    endsAt = DateTimeOffset.UtcNow.AddDays(14).AddHours(3),
                    countryCode = "GB",
                    cityName = "London",
                    venue = new { name = "Community Hall", addressLine1 = "1 High Street" }
                }),
            "eventId");

        var published = await HttpClient.PostAsJsonAsync(
            $"/api/v1/community-life/events/{eventId}/publish", new { });

        published.StatusCode.Should().Be(
            HttpStatusCode.OK, await published.Content.ReadAsStringAsync());

        return new PublishedEvent(eventId, organizer.User.Id, organizer.Tokens.AccessToken);
    }

    private async Task<PublishedProvider> PublishAProviderAsync()
    {
        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var providerId = await IdOfAsync(
            await HttpClient.PostAsJsonAsync(
                "/api/v1/providers",
                new
                {
                    name = $"Enrichment Plumbing {Guid.NewGuid():N}",
                    providerType = "Business",
                    category = "food-restaurants",
                    cityName = "London",
                    countryCode = "GB",
                    phone = "+447700900456"
                }),
            "providerId");

        var published = await HttpClient.PutAsJsonAsync(
            $"/api/v1/providers/{providerId}/status", new { status = "Active" });

        published.StatusCode.Should().Be(
            HttpStatusCode.OK, await published.Content.ReadAsStringAsync());

        return new PublishedProvider(providerId, owner.Tokens.AccessToken, owner.User.Id);
    }

    /// <summary>The most recent pending outbox payload of one contract, parsed.</summary>
    private async Task<JsonObject?> PendingPayloadAsync(string eventType)
    {
        var pending = await Factory.PendingOutboxAsync(eventType);

        return pending.Count == 0 ? null : JsonNode.Parse(pending[^1].Payload)!.AsObject();
    }

    private async Task<int> UnreadCountAsync()
    {
        var response = await HttpClient.GetAsync(
            new Uri("/api/v1/notifications/unread-count", UriKind.Relative));

        response.StatusCode.Should().Be(
            HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        return body["data"]!["total"]!.GetValue<int>();
    }

    /// <summary>Reads one identifier out of a response envelope, failing with the body if it is absent.</summary>
    private static async Task<Guid> IdOfAsync(HttpResponseMessage response, string name)
    {
        var text = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().BeOneOf([HttpStatusCode.OK, HttpStatusCode.Created], text);

        var data = JsonNode.Parse(text)!["data"];

        (data?[name]).Should().NotBeNull("'{0}' should be present in: {1}", name, text);

        return data![name]!.GetValue<Guid>();
    }
}
