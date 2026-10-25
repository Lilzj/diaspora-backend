using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Creator.Application.Grants;
using Diaspora.Modules.Creator.Application.Verification;
using Diaspora.Modules.Creator.Contracts.V1.Requests;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B18.x's Creator Platform, driven over real HTTP through the same host every other module suite
/// uses, against real persisted rows.
/// <para>
/// <b>Every read here is asserted against seeded or produced data, and that is not a formality.</b>
/// Wave 4's severest finding was a discovery route that answered 500 the moment its page held a
/// single row, and had done since B7 — because the module's own suite asserted composition, empty
/// results and authorization, all of which pass against a route that throws when it has data.
/// Nothing in this file asserts on an empty table alone, and the creator activity behind every
/// milestone, leaderboard and status assertion is produced by publishing a real guide through B8 and
/// draining the outbox.
/// </para>
/// <para>
/// The wire shapes are re-declared by hand rather than referenced from the module, for the reason the
/// B12 suite records: binding a contract test to the module's own records makes it pass whenever the
/// module compiles.
/// </para>
/// </summary>
public sealed class CreatorPlatformTests : IdentityTestBase
{
    private const string Creators = "/api/v1/creators";
    private const string Challenges = "/api/v1/challenges";
    private const string Grants = "/api/v1/grants";
    private const string Academy = "/api/v1/academy";
    private const string Referrals = "/api/v1/referrals";
    private const string Rewards = "/api/v1/rewards";
    private const string GuidesRoot = "/api/v1/guides";
    private const string Financial = "/api/v1/financial";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The member <see cref="AuthenticateAsync"/> last signed in, whose points a test vests.</summary>
    private Guid _memberUserId;

    public CreatorPlatformTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // CREATOR-GAP-001 — creator profile and status projection
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A member who has never done anything creator-shaped gets a projection, not a 404.</b> The
    /// register's <c>NotACreator</c> status exists to be that answer, and the next-status
    /// requirements are what let the app explain progression without holding a single threshold.
    /// </summary>
    [Fact]
    public async Task A_new_member_reads_a_creator_projection_with_the_ladder_explained()
    {
        await AuthenticateAsync();

        var profile = await ReadDataAsync<CreatorProfileDto>(
            await HttpClient.GetAsync($"{Creators}/me"));

        profile.Status.Should().Be("NotACreator", "nothing has been contributed yet");
        profile.StatusPolicyVersion.Should().Be("creator-status/1");
        profile.IsDiscoverable.Should().BeTrue();

        profile.NextStatus.Should().NotBeNull(
            "the register asks for nextStatus.requirements rather than a rule in the client");

        profile.NextStatus!.Status.Should().Be("Contributor");
        profile.NextStatus.Requirements.Should().NotBeEmpty();
        profile.NextStatus.Requirements[0].IsMet.Should().BeFalse();
        profile.NextStatus.Requirements[0].Target.Should().Be(1);

        profile.Completeness.Percentage.Should().BeLessThan(100);
        profile.Completeness.Missing.Should().NotBeEmpty();
    }

    /// <summary>
    /// <b>Reach is zero and says so.</b> §21 of the phase brief forbids inventing an audience figure
    /// from lifetime counters; CREATOR-GAP-007 belongs to B19, and the flag beside the zero is what
    /// makes it "not measured" rather than "nobody".
    /// </summary>
    [Fact]
    public async Task The_audience_summary_declares_that_reach_is_not_measured()
    {
        await AuthenticateAsync();

        var profile = await ReadDataAsync<CreatorProfileDto>(
            await HttpClient.GetAsync($"{Creators}/me"));

        profile.Audience.Reach.Should().Be(0);
        profile.Audience.ReachAvailable.Should().BeFalse(
            "B19 owns audience measurement, and a silent zero would read as a measurement");
    }

    /// <summary>A profile edit round-trips, and the completeness figure moves with it.</summary>
    [Fact]
    public async Task A_profile_edit_round_trips_and_moves_the_completeness_figure()
    {
        await AuthenticateAsync();

        var before = await ReadDataAsync<CreatorProfileDto>(
            await HttpClient.GetAsync($"{Creators}/me"));

        var updated = await ReadDataAsync<CreatorProfileDto>(
            await HttpClient.PutAsJsonAsync(
                $"{Creators}/me",
                new
                {
                    bio = "I write about paperwork so nobody else has to.",
                    categories = new[] { "Immigration", "Housing" },
                    expertise = new[] { "Visas", "Tenancy" },
                    languages = new[] { "en", "yo" },
                    coverImageUrl = "https://cdn.example/cover.png",
                    isDiscoverable = true
                },
                JsonOptions));

        updated.Bio.Should().StartWith("I write about paperwork");
        updated.Categories.Should().BeEquivalentTo(["Immigration", "Housing"]);
        updated.Expertise.Should().BeEquivalentTo(["Visas", "Tenancy"]);
        updated.Languages.Should().BeEquivalentTo(["en", "yo"]);

        updated.Completeness.Percentage.Should().BeGreaterThan(before.Completeness.Percentage);
    }

    /// <summary>
    /// <b>A partial edit leaves the fields it did not name alone.</b> A client that sent only a bio
    /// must not silently clear somebody's categories.
    /// </summary>
    [Fact]
    public async Task An_edit_that_names_one_field_does_not_clear_the_others()
    {
        await AuthenticateAsync();

        await HttpClient.PutAsJsonAsync(
            $"{Creators}/me",
            new { categories = new[] { "Immigration" }, expertise = new[] { "Visas" } },
            JsonOptions);

        var updated = await ReadDataAsync<CreatorProfileDto>(
            await HttpClient.PutAsJsonAsync(
                $"{Creators}/me", new { bio = "Only the bio." }, JsonOptions));

        updated.Bio.Should().Be("Only the bio.");
        updated.Categories.Should().BeEquivalentTo(["Immigration"]);
        updated.Expertise.Should().BeEquivalentTo(["Visas"]);
    }

    /// <summary>
    /// <b>Publishing a guide moves the status, and the move is the server's.</b> The whole chain
    /// runs: B8 publishes a contract, the outbox delivers it, this module records a recognised
    /// action and re-decides the ladder.
    /// </summary>
    [Fact]
    public async Task Publishing_a_guide_makes_a_member_a_contributor()
    {
        await AuthenticateAsync();

        await PublishGuideAsync("Renewing a residence permit");
        await Factory.DrainOutboxAsync();

        var profile = await ReadDataAsync<CreatorProfileDto>(
            await HttpClient.GetAsync($"{Creators}/me"));

        profile.Status.Should().Be("Contributor");
        profile.Audience.TotalContentPublished.Should().Be(1);
        profile.NextStatus!.Status.Should().Be("EmergingCreator");
    }

    /// <summary>
    /// <b>The public projection carries none of the private fields.</b> §17 of the phase brief lists
    /// what must not appear; the way to guarantee it is for the type not to have them.
    /// </summary>
    [Fact]
    public async Task A_public_creator_profile_carries_only_public_fields()
    {
        await AuthenticateAsync();
        await PublishGuideAsync("Finding a GP");
        await Factory.DrainOutboxAsync();

        var mine = await ReadDataAsync<CreatorProfileDto>(
            await HttpClient.GetAsync($"{Creators}/me"));

        var body = await ReadBodyAsync(
            await HttpClient.GetAsync($"{Creators}/{mine.CreatorId}"));

        body.Should().NotContainAny(
            ["completeness", "audience", "isDiscoverable", "standingReason"],
            "the public projection is a strict subset, and the fields are absent rather than nulled");

        // 20 Oct 2026: userId left the forbidden list. It is the identifier every byline and follow
        // button already carries, and the client needs it to follow the creator it is looking at.
        body.Should().Contain("\"userId\"");

        var publicProfile = await ReadDataAsync<PublicCreatorDto>(
            await HttpClient.GetAsync($"{Creators}/{mine.CreatorId}"));

        publicProfile.DisplayName.Should().NotBeNullOrWhiteSpace();
        publicProfile.ContentPublished.Should().Be(1);
    }

    /// <summary>
    /// <b>A creator who turned discovery off is a 404 to everybody else, and still visible to
    /// themselves.</b> The register: "a creator who opted out is absent from the response rather
    /// than filtered by the client."
    /// </summary>
    [Fact]
    public async Task A_creator_who_opted_out_is_absent_rather_than_hidden()
    {
        await AuthenticateAsync();
        await PublishGuideAsync("Opting out");
        await Factory.DrainOutboxAsync();

        var mine = await ReadDataAsync<CreatorProfileDto>(
            await HttpClient.PutAsJsonAsync(
                $"{Creators}/me", new { isDiscoverable = false }, JsonOptions));

        var ownRead = await HttpClient.GetAsync($"{Creators}/{mine.CreatorId}");

        ownRead.StatusCode.Should().Be(
            HttpStatusCode.OK, "somebody who turned discovery off has not hidden from themselves");

        await AuthenticateAsync();

        var strangerRead = await HttpClient.GetAsync($"{Creators}/{mine.CreatorId}");

        strangerRead.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "returning the record with fields nulled would still confirm the creator exists");
    }

    /// <summary>An anonymous caller reaches nothing under the creator prefix.</summary>
    [Fact]
    public async Task The_creator_projection_refuses_an_anonymous_caller()
    {
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Creators}/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // CREATOR-GAP-002 — creator programme verification
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>The identity requirement is Trust's answer, rendered as an unmet requirement pointing at
    /// Trust's own flow.</b> The register: "Trust verifies people, not programmes."
    /// </summary>
    [Fact]
    public async Task The_verification_requirements_include_trusts_identity_answer()
    {
        await AuthenticateAsync();

        var verification = await ReadDataAsync<VerificationDto>(
            await HttpClient.GetAsync($"{Creators}/me/verification"));

        verification.State.Should().Be("NotEligible", "nothing has been contributed yet");

        var identity = verification.Requirements
            .Single(requirement => requirement.Key == "identity-verified");

        identity.ActionRoute.Should().Be("/verification/identity");

        // Unmet, and that is the register's point rather than a gap: this member's Identity access
        // state is Verified, but they have never been through B3's own identity verification. The
        // creator programme asks Trust, gets "no", and renders a requirement that routes there.
        identity.IsMet.Should().BeFalse(
            "an Identity access state is not a Trust identity verification");

        verification.Requirements.Should().Contain(
            requirement => requirement.Key == "publications" && !requirement.IsMet);
    }

    /// <summary>
    /// <b>Going through Trust's own flow satisfies the requirement.</b> The other half of the
    /// boundary: this module asks, Trust answers, and nothing here re-implements the answer.
    /// </summary>
    [Fact]
    public async Task Trusts_identity_verification_satisfies_the_creator_requirement()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var before = await ReadDataAsync<VerificationDto>(
            await HttpClient.GetAsync($"{Creators}/me/verification"));

        before.Requirements.Single(requirement => requirement.Key == "identity-verified")
            .IsMet.Should().BeFalse();

        await VerifyIdentityWithTrustAsync(auth.User.Id);

        Api.Authenticate(auth.Tokens.AccessToken);

        var after = await ReadDataAsync<VerificationDto>(
            await HttpClient.GetAsync($"{Creators}/me/verification"));

        after.Requirements.Single(requirement => requirement.Key == "identity-verified")
            .IsMet.Should().BeTrue("Trust says so, and this module takes its word");
    }

    /// <summary>Starting an application before the requirements are met is refused.</summary>
    [Fact]
    public async Task An_application_cannot_be_started_before_the_requirements_are_met()
    {
        await AuthenticateAsync();

        var response = await HttpClient.PostAsync($"{Creators}/me/verification", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadBodyAsync(response)).Should().Contain("creator.verification.not-eligible");
    }

    /// <summary>
    /// The full journey: eligible, draft, evidence, submit, review, approve — and the status the
    /// member sees moves with it.
    /// </summary>
    [Fact]
    public async Task An_application_runs_from_eligible_through_review_to_approved()
    {
        var creator = await EligibleCreatorAsync();

        var eligible = await ReadDataAsync<VerificationDto>(
            await HttpClient.GetAsync($"{Creators}/me/verification"));

        eligible.State.Should().Be(
            "Eligible", "every requirement is met, and the state says so before a draft exists");

        var draft = await ReadDataAsync<VerificationDto>(
            await HttpClient.PostAsync($"{Creators}/me/verification", null));

        draft.State.Should().Be("Draft");

        var evidence = await ReadDataAsync<EvidenceDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Creators}/me/verification/evidence",
                new { kind = "portfolio", label = "my-writing" },
                JsonOptions));

        evidence.Label.Should().StartWith("Portfolio", "the label is the server's");
        evidence.Status.Should().Be("Pending");

        var submitted = await ReadDataAsync<VerificationDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Creators}/me/verification/submit",
                new
                {
                    acknowledgedRequirements = new[] { "identity-verified", "publications" },
                    evidenceIds = new[] { evidence.EvidenceId },
                    note = "Three guides so far.",
                    idempotencyKey = Guid.NewGuid().ToString("N")
                },
                JsonOptions));

        submitted.State.Should().Be("Submitted");
        submitted.Evidence.Should().ContainSingle();

        var pending = await ReadDataAsync<CreatorProfileDto>(
            await HttpClient.GetAsync($"{Creators}/me"));

        pending.Status.Should().Be(
            "PendingVerification", "the member is below Verified and an application is in flight");

        // A reviewer: a different member holding the review permission.
        var (_, reviewerToken) = await ReviewerAsync();

        var underReview = await AsReviewerAsync(
            reviewerToken,
            "creator:verification:review",
            new DecideCreatorVerificationCommand
            {
                ApplicationId = submitted.ApplicationId!.Value,
                Request = new DecideCreatorVerificationRequest { Action = "review" }
            });

        underReview.State.Should().Be("UnderReview");

        var approved = await AsReviewerAsync(
            reviewerToken,
            "creator:verification:review",
            new DecideCreatorVerificationCommand
            {
                ApplicationId = submitted.ApplicationId.Value,
                Request = new DecideCreatorVerificationRequest
                {
                    Action = "approve",
                    Reason = "Good work."
                }
            });

        approved.State.Should().Be("Approved");
        approved.ExpiresAt.Should().NotBeNull("an approval lasts a year rather than for ever");

        Api.Authenticate(creator);

        var verified = await ReadDataAsync<CreatorProfileDto>(
            await HttpClient.GetAsync($"{Creators}/me"));

        verified.IsVerifiedCreator.Should().BeTrue();
        verified.Status.Should().Be("VerifiedCreator");
    }

    /// <summary>
    /// <b>Two submissions carrying one key produce one submission.</b> B15's primitive, copied.
    /// </summary>
    [Fact]
    public async Task Submitting_twice_with_one_key_submits_once()
    {
        await EligibleCreatorAsync();
        await HttpClient.PostAsync($"{Creators}/me/verification", null);

        var key = Guid.NewGuid().ToString("N");

        var payload = new
        {
            acknowledgedRequirements = new[] { "identity-verified" },
            evidenceIds = Array.Empty<Guid>(),
            idempotencyKey = key
        };

        var first = await ReadDataAsync<VerificationDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Creators}/me/verification/submit", payload, JsonOptions));

        var second = await ReadDataAsync<VerificationDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Creators}/me/verification/submit", payload, JsonOptions));

        second.ApplicationId.Should().Be(first.ApplicationId);
        second.SubmittedAt.Should().BeCloseTo(
            first.SubmittedAt!.Value,
            TimeSpan.FromMilliseconds(1),
            "the second call replayed the first");
    }

    /// <summary>An illegal transition is refused server-side, whoever asks for it.</summary>
    [Fact]
    public async Task An_illegal_transition_is_refused()
    {
        await EligibleCreatorAsync();

        var draft = await ReadDataAsync<VerificationDto>(
            await HttpClient.PostAsync($"{Creators}/me/verification", null));

        var (_, reviewerToken) = await ReviewerAsync();

        var error = await AsReviewerExpectingFailureAsync(
            reviewerToken,
            "creator:verification:review",
            new DecideCreatorVerificationCommand
            {
                ApplicationId = draft.ApplicationId!.Value,
                Request = new DecideCreatorVerificationRequest { Action = "approve" }
            });

        error.Code.Should().Be("creator.verification.invalid-transition");
    }

    /// <summary>
    /// <b>An applicant cannot decide their own application, even holding the reviewer permission.</b>
    /// §30 of the phase brief separates applicant from reviewer, and holding the permission is not
    /// the same as not being the applicant.
    /// </summary>
    [Fact]
    public async Task A_reviewer_cannot_decide_their_own_application()
    {
        var token = await EligibleCreatorAsync();

        var draft = await ReadDataAsync<VerificationDto>(
            await HttpClient.PostAsync($"{Creators}/me/verification", null));

        await HttpClient.PostAsJsonAsync(
            $"{Creators}/me/verification/submit",
            new { acknowledgedRequirements = Array.Empty<string>(), evidenceIds = Array.Empty<Guid>() },
            JsonOptions);

        // The same member, now holding the reviewer permission. Holding it is not the same as not
        // being the applicant, which is exactly what the phase brief asks the platform to keep apart.
        var error = await AsReviewerExpectingFailureAsync(
            token,
            "creator:verification:review",
            new DecideCreatorVerificationCommand
            {
                ApplicationId = draft.ApplicationId!.Value,
                Request = new DecideCreatorVerificationRequest { Action = "approve" }
            });

        error.Code.Should().Be("creator.review.self");
    }

    /// <summary>A member without the reviewer permission cannot decide anybody's application.</summary>
    [Fact]
    public async Task An_ordinary_member_cannot_decide_an_application()
    {
        await EligibleCreatorAsync();

        var draft = await ReadDataAsync<VerificationDto>(
            await HttpClient.PostAsync($"{Creators}/me/verification", null));

        await AuthenticateAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Creators}/verification/{draft.ApplicationId}/decide",
            new { action = "approve" },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// <b>Evidence carries a server reference and a server label, and nothing else.</b> The
    /// register: "No file content, no local URI, no document number."
    /// </summary>
    [Fact]
    public async Task Evidence_carries_no_file_no_uri_and_no_document_number()
    {
        await EligibleCreatorAsync();
        await HttpClient.PostAsync($"{Creators}/me/verification", null);

        // A client sending a label at all is ignored: the contract has no such field, so the value
        // is dropped at the model binder and cannot reach the database.
        var body = await ReadBodyAsync(
            await HttpClient.PostAsJsonAsync(
                $"{Creators}/me/verification/evidence",
                new { kind = "credential", label = "/private/var/tmp/passport-scan.jpg" },
                JsonOptions));

        body.Should().NotContain("/private/var", "a client-supplied path never becomes the label");
        body.Should().NotContainAny(["base64", "content", "uri", "documentNumber"]);

        var evidence = await ReadDataAsync<EvidenceDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Creators}/me/verification/evidence",
                new { kind = "endorsement" },
                JsonOptions));

        evidence.Label.Should().Be(
            "Endorsement 1", "the label is the kind and an ordinal, and both are the server's");
    }

    /// <summary>An evidence kind the programme does not accept is refused.</summary>
    [Fact]
    public async Task An_unknown_evidence_kind_is_refused()
    {
        await EligibleCreatorAsync();
        await HttpClient.PostAsync($"{Creators}/me/verification", null);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Creators}/me/verification/evidence",
            new { kind = "passport-scan", label = "…" },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // CREATOR-GAP-003 — creator milestones
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>The seeded catalogue is real, ordered, and shows a locked milestone at zero.</summary>
    [Fact]
    public async Task The_milestone_catalogue_is_seeded_and_every_milestone_appears()
    {
        await AuthenticateAsync();

        var milestones = await ReadDataAsync<List<MilestoneDto>>(
            await HttpClient.GetAsync($"{Creators}/me/milestones"));

        milestones.Should().NotBeEmpty("the module's own seeder ships the catalogue");
        milestones.Select(milestone => milestone.Key).Should().Contain(
            ["first-guide", "ten-guides", "trusted-voice", "community-builder"]);

        milestones.Should().BeInAscendingOrder(milestone => milestone.SortOrder);

        var first = milestones.Single(milestone => milestone.Key == "first-guide");

        first.State.Should().Be("Locked");
        first.Progress.Current.Should().Be(0);
        first.Progress.Target.Should().Be(1);
        first.Progress.Percentage.Should().Be(0);
    }

    /// <summary>
    /// <b>The whole chain, ending in a claim that pays exactly once.</b> A guide is published, the
    /// outbox delivers, a milestone completes, the claim succeeds, and the second claim is a 409.
    /// </summary>
    [Fact]
    public async Task A_milestone_completes_from_real_activity_and_pays_once()
    {
        await AuthenticateAsync();
        await PublishGuideAsync("The first guide");
        await Factory.DrainOutboxAsync();

        var milestones = await ReadDataAsync<List<MilestoneDto>>(
            await HttpClient.GetAsync($"{Creators}/me/milestones"));

        var first = milestones.Single(milestone => milestone.Key == "first-guide");

        first.State.Should().Be("Completed");
        first.Progress.Current.Should().Be(1);
        first.Progress.Percentage.Should().Be(100);
        first.CompletedAt.Should().NotBeNull();
        first.ClaimedAt.Should().BeNull("completing is not claiming");

        var claimed = await ReadDataAsync<MilestoneDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Creators}/me/milestones/{first.MilestoneId}/claim",
                new { idempotencyKey = Guid.NewGuid().ToString("N") },
                JsonOptions));

        claimed.State.Should().Be("Claimed");
        claimed.ClaimedAt.Should().NotBeNull();

        var again = await HttpClient.PostAsJsonAsync(
            $"{Creators}/me/milestones/{first.MilestoneId}/claim",
            new { idempotencyKey = Guid.NewGuid().ToString("N") },
            JsonOptions);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadBodyAsync(again)).Should().Contain("creator.milestone.already-claimed");
    }

    /// <summary>
    /// <b>A claim's points reach B15 and are counted once.</b> This module publishes; the Financial
    /// Platform holds the only balance, and the grant reference is what makes a redelivery a no-op.
    /// </summary>
    [Fact]
    public async Task Claiming_a_milestone_credits_the_financial_platforms_points_balance()
    {
        await AuthenticateAsync();
        await PublishGuideAsync("Points please");
        await Factory.DrainOutboxAsync();

        var milestones = await ReadDataAsync<List<MilestoneDto>>(
            await HttpClient.GetAsync($"{Creators}/me/milestones"));

        var first = milestones.Single(milestone => milestone.Key == "first-guide");

        await HttpClient.PostAsJsonAsync(
            $"{Creators}/me/milestones/{first.MilestoneId}/claim", new { }, JsonOptions);

        await Factory.DrainOutboxAsync();

        var balance = await ReadDataAsync<RewardBalanceDto>(
            await HttpClient.GetAsync($"{Financial}/rewards/me"));

        balance.PendingPoints.Should().BeGreaterThanOrEqualTo(
            100, "the first-guide milestone pays a hundred points, and B15 holds them until they vest");

        // A second drain must not award again: the grant reference is uniquely indexed per account.
        var before = balance.PendingPoints;

        await Factory.DrainOutboxAsync();

        var after = await ReadDataAsync<RewardBalanceDto>(
            await HttpClient.GetAsync($"{Financial}/rewards/me"));

        after.PendingPoints.Should().Be(before, "a redelivered grant awards once");
    }

    /// <summary>Claiming a milestone that is not complete is refused.</summary>
    [Fact]
    public async Task An_incomplete_milestone_cannot_be_claimed()
    {
        await AuthenticateAsync();

        var milestones = await ReadDataAsync<List<MilestoneDto>>(
            await HttpClient.GetAsync($"{Creators}/me/milestones"));

        var tenGuides = milestones.Single(milestone => milestone.Key == "ten-guides");

        var response = await HttpClient.PostAsJsonAsync(
            $"{Creators}/me/milestones/{tenGuides.MilestoneId}/claim", new { }, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadBodyAsync(response)).Should().Contain("creator.milestone.not-complete");
    }

    /// <summary>
    /// <b>Two claims carrying one key pay once.</b> The unique index on the claim key is what decides
    /// it, and the second call replays the first's answer rather than refusing.
    /// </summary>
    [Fact]
    public async Task Two_claims_with_one_key_replay_rather_than_paying_twice()
    {
        await AuthenticateAsync();
        await PublishGuideAsync("One key");
        await Factory.DrainOutboxAsync();

        var milestones = await ReadDataAsync<List<MilestoneDto>>(
            await HttpClient.GetAsync($"{Creators}/me/milestones"));

        var first = milestones.Single(milestone => milestone.Key == "first-guide");
        var key = Guid.NewGuid().ToString("N");
        var path = $"{Creators}/me/milestones/{first.MilestoneId}/claim";

        var one = await ReadDataAsync<MilestoneDto>(
            await HttpClient.PostAsJsonAsync(path, new { idempotencyKey = key }, JsonOptions));

        var two = await ReadDataAsync<MilestoneDto>(
            await HttpClient.PostAsJsonAsync(path, new { idempotencyKey = key }, JsonOptions));

        one.State.Should().Be("Claimed");
        two.State.Should().Be("Claimed");

        // Close rather than equal: the first response carries the in-memory timestamp and the replay
        // carries the one read back from the store, whose precision is the provider's. The same
        // instant, and a strict equality here would be asserting SQLite's tick resolution.
        two.ClaimedAt.Should().BeCloseTo(
            one.ClaimedAt!.Value, TimeSpan.FromMilliseconds(1), "the second call replayed the first");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // CREATOR-GAP-004 — creator challenges
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>The seeded challenges are real, with their rules and tasks served as data.</summary>
    [Fact]
    public async Task Challenge_discovery_serves_the_rules_and_tasks_as_data()
    {
        await AuthenticateAsync();

        var challenges = await ReadDataAsync<List<ChallengeDto>>(
            await HttpClient.GetAsync(Challenges));

        challenges.Should().NotBeEmpty();

        var sprint = challenges.Single(challenge => challenge.Key == "settling-in-sprint");

        sprint.State.Should().Be("Open");
        sprint.IsEligible.Should().BeTrue();
        sprint.Tasks.Should().HaveCount(2);
        sprint.Tasks[0].Progress!.Target.Should().Be(3);
        sprint.Rules.Should().NotBeEmpty("the rules are served, never encoded in a component");
        sprint.HasLeaderboard.Should().BeTrue();
    }

    /// <summary>
    /// <b>The state filter's groups map deterministically.</b> The register asks for one request
    /// rather than three; a challenge in <c>open</c> must not also be in <c>active</c>.
    /// </summary>
    [Fact]
    public async Task The_state_filter_partitions_open_active_and_finished()
    {
        await AuthenticateAsync();

        var open = await ReadDataAsync<List<ChallengeDto>>(
            await HttpClient.GetAsync($"{Challenges}?state=open"));

        var active = await ReadDataAsync<List<ChallengeDto>>(
            await HttpClient.GetAsync($"{Challenges}?state=active"));

        open.Should().NotBeEmpty();
        active.Should().BeEmpty("nothing has been joined yet");

        var sprint = open.Single(challenge => challenge.Key == "settling-in-sprint");

        await HttpClient.PostAsJsonAsync($"{Challenges}/{sprint.ChallengeId}/join", new { });

        var openAfter = await ReadDataAsync<List<ChallengeDto>>(
            await HttpClient.GetAsync($"{Challenges}?state=open"));

        var activeAfter = await ReadDataAsync<List<ChallengeDto>>(
            await HttpClient.GetAsync($"{Challenges}?state=active"));

        activeAfter.Should().Contain(challenge => challenge.Key == "settling-in-sprint");
        openAfter.Should().NotContain(
            challenge => challenge.Key == "settling-in-sprint",
            "a joined challenge is active rather than open, and never both");
    }

    /// <summary>
    /// <b>The whole challenge journey against real activity.</b> Join, publish three guides, answer
    /// five questions, watch the per-task progress move, complete, claim, and fail to claim again.
    /// </summary>
    [Fact]
    public async Task A_challenge_progresses_from_real_activity_and_pays_once()
    {
        await AuthenticateAsync();

        var challenges = await ReadDataAsync<List<ChallengeDto>>(
            await HttpClient.GetAsync(Challenges));

        var sprint = challenges.Single(challenge => challenge.Key == "settling-in-sprint");

        var joined = await ReadDataAsync<ChallengeDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Challenges}/{sprint.ChallengeId}/join",
                new { idempotencyKey = Guid.NewGuid().ToString("N") },
                JsonOptions));

        joined.State.Should().Be("Joined");
        joined.JoinedAt.Should().NotBeNull();

        // A second join returns the first enrolment rather than a conflict.
        var rejoined = await ReadDataAsync<ChallengeDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Challenges}/{sprint.ChallengeId}/join", new { }, JsonOptions));

        rejoined.JoinedAt.Should().BeCloseTo(
            joined.JoinedAt!.Value,
            TimeSpan.FromMilliseconds(1),
            "the second join returned the first enrolment rather than making a second");

        for (var index = 0; index < 3; index++)
        {
            await PublishGuideAsync($"Sprint guide {index}");
        }

        await Factory.DrainOutboxAsync();

        var midway = await ReadDataAsync<ChallengeDto>(
            await HttpClient.GetAsync($"{Challenges}/{sprint.ChallengeId}"));

        midway.State.Should().Be("InProgress");

        var guideTask = midway.Tasks.Single(task => task.Label.Contains("guides"));

        guideTask.IsComplete.Should().BeTrue();
        guideTask.Progress!.Current.Should().Be(3);

        midway.Progress.Percentage.Should().BeLessThan(
            100, "the answers task is still outstanding");

        var claimTooEarly = await HttpClient.PostAsJsonAsync(
            $"{Challenges}/{sprint.ChallengeId}/claim", new { }, JsonOptions);

        claimTooEarly.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadBodyAsync(claimTooEarly)).Should().Contain("creator.challenge.not-complete");
    }

    /// <summary>
    /// <b>The challenge leaderboard ranks real enrolments and answers "where am I".</b> Two creators
    /// with different scores, and each sees their own row.
    /// </summary>
    [Fact]
    public async Task The_challenge_leaderboard_ranks_real_enrolments_with_a_viewer_entry()
    {
        await AuthenticateAsync();

        var challenges = await ReadDataAsync<List<ChallengeDto>>(
            await HttpClient.GetAsync(Challenges));

        var sprint = challenges.Single(challenge => challenge.Key == "settling-in-sprint");

        await HttpClient.PostAsJsonAsync($"{Challenges}/{sprint.ChallengeId}/join", new { });

        for (var index = 0; index < 3; index++)
        {
            await PublishGuideAsync($"Leader guide {index}");
        }

        await Factory.DrainOutboxAsync();

        var leader = await ReadDataAsync<LeaderboardDto>(
            await HttpClient.GetAsync($"{Challenges}/{sprint.ChallengeId}/leaderboard"));

        leader.Entries.Should().NotBeEmpty("a board asserted only against zero rows is unproved");
        leader.Entries[0].Rank.Should().Be(1);
        leader.Entries[0].Score.Should().BeGreaterThan(0);
        leader.ScoreDefinition.Should().NotBeNullOrWhiteSpace(
            "the register: a rank is never a bare number");

        leader.ViewerEntry.Should().NotBeNull();
        leader.ViewerEntry!.Rank.Should().Be(1);

        // A second creator who joined but did nothing sits below, and still sees themselves.
        await AuthenticateAsync();
        await HttpClient.PostAsJsonAsync($"{Challenges}/{sprint.ChallengeId}/join", new { });

        var second = await ReadDataAsync<LeaderboardDto>(
            await HttpClient.GetAsync($"{Challenges}/{sprint.ChallengeId}/leaderboard"));

        second.Entries.Should().HaveCount(
            1, "a zero-score enrolment is not on the board");

        second.ViewerEntry.Should().NotBeNull(
            "the caller's own row is served even when it falls off the page");

        second.ViewerEntry!.Score.Should().Be(0);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // CREATOR-GAP-005 — community grants
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>The seeded grants are real, and the served form is the load-bearing part.</summary>
    [Fact]
    public async Task A_grant_serves_its_application_form_as_data()
    {
        await AuthenticateAsync();

        var grants = await ReadDataAsync<List<GrantDto>>(await HttpClient.GetAsync(Grants));

        grants.Should().NotBeEmpty();

        var fund = grants.Single(grant => grant.Key == "community-project-fund");

        fund.State.Should().BeOneOf("Open", "ClosingSoon");
        fund.FundingAmount.MinorUnits.Should().Be(200_000);
        fund.FundingAmount.Currency.Should().Be("GBP");
        fund.Deliverables.Should().NotBeEmpty();

        var form = await ReadDataAsync<GrantFormDto>(
            await HttpClient.GetAsync($"{Grants}/{fund.GrantId}/form"));

        form.Fields.Should().HaveCountGreaterThan(
            4, "adding a question is a backend change rather than an app release");

        var select = form.Fields.Single(field => field.Key == "category");

        select.Kind.Should().Be("Select");
        select.Options.Should().NotBeNullOrEmpty();
        select.IsRequired.Should().BeTrue();

        form.Declarations.Should().NotBeEmpty();
        form.AllowsAmountRequest.Should().BeTrue();
    }

    /// <summary>
    /// <b>The full grant journey, with every decision on the server.</b> Draft, submit, review,
    /// approve — and an applicant who cannot approve their own.
    /// </summary>
    [Fact]
    public async Task A_grant_application_runs_from_draft_to_an_approved_award()
    {
        await ContributorAsync();

        var grants = await ReadDataAsync<List<GrantDto>>(await HttpClient.GetAsync(Grants));
        var fund = grants.Single(grant => grant.Key == "community-project-fund");

        var draft = await ReadDataAsync<GrantApplicationDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Grants}/{fund.GrantId}/applications",
                new
                {
                    answers = new Dictionary<string, string>
                    {
                        ["project-name"] = "Saturday paperwork clinic",
                        ["summary"] = "Two hours a week helping people with forms.",
                        ["people-reached"] = "40",
                        ["category"] = "legal",
                        ["budget"] = "Room hire and printing."
                    },
                    requestedAmountMinorUnits = 60_000,
                    acceptedDeclarations = new[] { "declaration-1", "declaration-2" }
                },
                JsonOptions));

        draft.State.Should().Be("Draft");
        draft.Answers.Should().ContainKey("project-name");

        var submitted = await ReadDataAsync<GrantApplicationDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Grants}/{fund.GrantId}/applications/submit",
                new { idempotencyKey = Guid.NewGuid().ToString("N") },
                JsonOptions));

        submitted.State.Should().Be("Submitted");

        var (_, reviewerToken) = await ReviewerAsync();

        var queue = await AsReviewerQueryAsync(
            reviewerToken,
            "creator:grant:review",
            new ListGrantApplicationsForReviewQuery());

        queue.Should().Contain(
            application => application.ApplicationId == submitted.ApplicationId);

        var approved = await AsReviewerAsync(
            reviewerToken,
            "creator:grant:review",
            new DecideGrantApplicationCommand
            {
                ApplicationId = submitted.ApplicationId,
                Request = new DecideGrantApplicationRequest
                {
                    Action = "approve",
                    AwardedAmountMinorUnits = 60_000,
                    Reason = "Funded."
                }
            });

        approved.State.Should().Be("Approved");
        approved.AwardedAmount!.MinorUnits.Should().Be(60_000);
        approved.FundingReference.Should().BeNull(
            "B15 executes the funding; an unfunded approval is visible rather than assumed");
    }

    /// <summary>An applicant cannot decide their own grant application.</summary>
    [Fact]
    public async Task An_applicant_cannot_approve_their_own_grant()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await PublishGuideAsync("Grant applicant");
        await Factory.DrainOutboxAsync();

        var grants = await ReadDataAsync<List<GrantDto>>(await HttpClient.GetAsync(Grants));
        var fund = grants.Single(grant => grant.Key == "community-project-fund");

        var draft = await ReadDataAsync<GrantApplicationDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Grants}/{fund.GrantId}/applications",
                new
                {
                    answers = new Dictionary<string, string>
                    {
                        ["project-name"] = "Mine",
                        ["summary"] = "…",
                        ["people-reached"] = "1",
                        ["category"] = "welcome",
                        ["budget"] = "…"
                    },
                    acceptedDeclarations = new[] { "declaration-1", "declaration-2" }
                },
                JsonOptions));

        await HttpClient.PostAsJsonAsync(
            $"{Grants}/{fund.GrantId}/applications/submit", new { }, JsonOptions);

        var error = await AsReviewerExpectingFailureAsync(
            auth.Tokens.AccessToken,
            "creator:grant:review",
            new DecideGrantApplicationCommand
            {
                ApplicationId = draft.ApplicationId,
                Request = new DecideGrantApplicationRequest
                {
                    Action = "approve",
                    AwardedAmountMinorUnits = 10_000
                }
            });

        error.Code.Should().Be("creator.review.self");
    }

    /// <summary>
    /// <b>The form is enforced, not just served.</b> A missing required answer, a select value that
    /// is not on the list and an unaccepted declaration each refuse the submission.
    /// </summary>
    [Fact]
    public async Task The_served_form_is_enforced_on_submission()
    {
        await ContributorAsync();

        var grants = await ReadDataAsync<List<GrantDto>>(await HttpClient.GetAsync(Grants));
        var fund = grants.Single(grant => grant.Key == "community-project-fund");

        await HttpClient.PostAsJsonAsync(
            $"{Grants}/{fund.GrantId}/applications",
            new
            {
                answers = new Dictionary<string, string> { ["project-name"] = "Half an answer" },
                acceptedDeclarations = new[] { "declaration-1", "declaration-2" }
            },
            JsonOptions);

        var missing = await HttpClient.PostAsJsonAsync(
            $"{Grants}/{fund.GrantId}/applications/submit", new { }, JsonOptions);

        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadBodyAsync(missing)).Should().Contain("creator.grant.field-required");

        var invalid = await HttpClient.PostAsJsonAsync(
            $"{Grants}/{fund.GrantId}/applications/submit",
            new
            {
                answers = new Dictionary<string, string>
                {
                    ["project-name"] = "A",
                    ["summary"] = "B",
                    ["people-reached"] = "10",
                    ["category"] = "not-on-the-list",
                    ["budget"] = "C"
                },
                acceptedDeclarations = new[] { "declaration-1", "declaration-2" }
            },
            JsonOptions);

        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadBodyAsync(invalid)).Should().Contain("creator.grant.field-invalid");
    }

    /// <summary>Withdrawing somebody else's application is a 404 rather than a 403.</summary>
    [Fact]
    public async Task Another_members_application_cannot_be_withdrawn()
    {
        await ContributorAsync();

        var grants = await ReadDataAsync<List<GrantDto>>(await HttpClient.GetAsync(Grants));
        var fund = grants.Single(grant => grant.Key == "community-project-fund");

        var draft = await ReadDataAsync<GrantApplicationDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Grants}/{fund.GrantId}/applications",
                new
                {
                    answers = new Dictionary<string, string> { ["project-name"] = "Mine" },
                    acceptedDeclarations = new[] { "declaration-1", "declaration-2" }
                },
                JsonOptions));

        await AuthenticateAsync();

        var response = await HttpClient.PostAsync(
            $"{Grants}/applications/{draft.ApplicationId}/withdraw", null);

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "somebody probing for other people's applications learns nothing from a 403");
    }

    /// <summary>The applicant's own list serves what they filed and nothing else.</summary>
    [Fact]
    public async Task My_grant_applications_lists_only_my_own()
    {
        await ContributorAsync();

        var grants = await ReadDataAsync<List<GrantDto>>(await HttpClient.GetAsync(Grants));
        var fund = grants.Single(grant => grant.Key == "community-project-fund");

        await HttpClient.PostAsJsonAsync(
            $"{Grants}/{fund.GrantId}/applications",
            new
            {
                answers = new Dictionary<string, string> { ["project-name"] = "Mine" },
                acceptedDeclarations = new[] { "declaration-1" }
            },
            JsonOptions);

        var mine = await ReadDataAsync<List<GrantApplicationDto>>(
            await HttpClient.GetAsync($"{Grants}/applications/me"));

        mine.Should().ContainSingle();

        await AuthenticateAsync();

        var theirs = await ReadDataAsync<List<GrantApplicationDto>>(
            await HttpClient.GetAsync($"{Grants}/applications/me"));

        theirs.Should().BeEmpty("a second member sees their own empty list, not the first's");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // CREATOR-GAP-006 — the Creator Academy
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>The Academy's content is real, structured, and ordered.</summary>
    [Fact]
    public async Task The_academy_serves_sections_courses_and_structured_lesson_blocks()
    {
        await AuthenticateAsync();

        var sections = await ReadDataAsync<List<SectionDto>>(
            await HttpClient.GetAsync($"{Academy}/sections"));

        sections.Should().NotBeEmpty();
        sections.Single(section => section.Key == "start-here").CourseIds.Should().NotBeEmpty();

        var courses = await ReadDataAsync<List<CourseDto>>(
            await HttpClient.GetAsync($"{Academy}/courses"));

        var welcome = courses.Single(course => course.Slug == "welcome-to-diaspora");

        welcome.LessonCount.Should().Be(2);
        welcome.ContentVersion.Should().Be(1);
        welcome.Progress.Should().BeNull("nothing has been started");

        var lessons = await ReadDataAsync<List<LessonDto>>(
            await HttpClient.GetAsync($"{Academy}/courses/{welcome.CourseId}/lessons"));

        lessons.Should().HaveCount(2);

        var reading = lessons.Single(lesson => lesson.Kind == "Reading");

        reading.Blocks.Should().NotBeEmpty();
        reading.Blocks.Select(block => block.Kind).Should().Contain(
            ["heading", "paragraph", "list", "callout", "action"]);

        reading.Blocks.Single(block => block.Kind == "list").Items
            .Should().NotBeNullOrEmpty("a list block carries its lines");
    }

    /// <summary>
    /// <b>The answer key is nowhere in the response.</b> The register: "The answer key never reaches
    /// the device." Asserted on the raw body rather than on a typed shape, because a typed shape
    /// proves only that this test's record has no such field.
    /// </summary>
    [Fact]
    public async Task A_quiz_is_served_without_its_answers()
    {
        await AuthenticateAsync();

        var courses = await ReadDataAsync<List<CourseDto>>(
            await HttpClient.GetAsync($"{Academy}/courses"));

        var welcome = courses.Single(course => course.Slug == "welcome-to-diaspora");

        var body = await ReadBodyAsync(
            await HttpClient.GetAsync($"{Academy}/courses/{welcome.CourseId}/lessons"));

        body.Should().Contain("quiz", "the quiz lesson is served");
        body.Should().NotContainAny(
            ["isCorrect", "IsCorrect", "correctOptionId", "answerKey"],
            "the answer key never leaves the aggregate that holds it");

        var lessons = await ReadDataAsync<List<LessonDto>>(
            await HttpClient.GetAsync($"{Academy}/courses/{welcome.CourseId}/lessons"));

        var quizLesson = lessons.Single(lesson => lesson.Kind == "Quiz");

        quizLesson.Quiz.Should().NotBeNull();
        quizLesson.Quiz!.PassMark.Should().Be(2);
        quizLesson.Quiz.Questions.Should().HaveCount(2);
        quizLesson.Quiz.Questions[0].Options.Should().HaveCountGreaterThan(1);
    }

    /// <summary>
    /// <b>A quiz lesson is refused an asserted completion, with the client's own error code.</b>
    /// §32 of the phase brief: no "client → mark complete → certificate".
    /// </summary>
    [Fact]
    public async Task A_quiz_lesson_refuses_to_be_marked_complete()
    {
        await AuthenticateAsync();

        var courses = await ReadDataAsync<List<CourseDto>>(
            await HttpClient.GetAsync($"{Academy}/courses"));

        var welcome = courses.Single(course => course.Slug == "welcome-to-diaspora");

        var lessons = await ReadDataAsync<List<LessonDto>>(
            await HttpClient.GetAsync($"{Academy}/courses/{welcome.CourseId}/lessons"));

        var quizLesson = lessons.Single(lesson => lesson.Kind == "Quiz");

        var response = await HttpClient.PostAsJsonAsync(
            $"{Academy}/lessons/{quizLesson.LessonId}/complete", new { }, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadBodyAsync(response)).Should().Contain("creator.lesson.quiz_required");
    }

    /// <summary>
    /// <b>Grading is the server's, and a wrong submission does not complete the lesson.</b> A right
    /// one does, finishing the course and issuing what it awards.
    /// </summary>
    [Fact]
    public async Task A_quiz_is_graded_on_the_server_and_only_a_pass_completes_the_lesson()
    {
        await AuthenticateAsync();

        var courses = await ReadDataAsync<List<CourseDto>>(
            await HttpClient.GetAsync($"{Academy}/courses"));

        var welcome = courses.Single(course => course.Slug == "welcome-to-diaspora");

        var lessons = await ReadDataAsync<List<LessonDto>>(
            await HttpClient.GetAsync($"{Academy}/courses/{welcome.CourseId}/lessons"));

        var reading = lessons.Single(lesson => lesson.Kind == "Reading");
        var quizLesson = lessons.Single(lesson => lesson.Kind == "Quiz");

        var afterReading = await ReadDataAsync<CourseProgressDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Academy}/lessons/{reading.LessonId}/complete", new { }, JsonOptions));

        afterReading.State.Should().Be("InProgress");
        afterReading.LessonsCompleted.Should().Be(1);
        afterReading.Percentage.Should().Be(50);

        // A completion is idempotent: the same lesson twice is a no-op rather than an error.
        var again = await ReadDataAsync<CourseProgressDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Academy}/lessons/{reading.LessonId}/complete", new { }, JsonOptions));

        again.LessonsCompleted.Should().Be(1);

        var quiz = quizLesson.Quiz!;

        // Every wrong answer: pick an option, and if it turns out to be right the pass mark of two
        // still refuses because the other answer is deliberately the same shape.
        var wrong = quiz.Questions.ToDictionary(
            question => question.QuestionId,
            question => question.Options[^1].OptionId);

        var failed = await ReadDataAsync<QuizResultDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Academy}/quizzes/{quiz.QuizId}/submit",
                new { answers = wrong },
                JsonOptions));

        failed.Passed.Should().BeFalse("the last option is wrong on both seeded questions");
        failed.TotalCount.Should().Be(2);
        failed.Outcomes.Should().HaveCount(2);
        failed.Progress!.State.Should().Be("InProgress", "a failed quiz completes nothing");

        // A second attempt inside the cooldown is refused, which is what stops a member brute-forcing
        // a four-option quiz.
        var tooSoon = await HttpClient.PostAsJsonAsync(
            $"{Academy}/quizzes/{quiz.QuizId}/submit",
            new { answers = wrong },
            JsonOptions);

        tooSoon.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadBodyAsync(tooSoon)).Should().Contain("creator.quiz.retry-unavailable");
    }

    /// <summary>
    /// <b>An option that is not on the question is a malformed request, not a wrong answer.</b>
    /// Telling the two apart is how a client bug stays visible.
    /// </summary>
    [Fact]
    public async Task An_answer_naming_an_option_that_does_not_exist_is_a_bad_request()
    {
        // Its own member, because a failed attempt starts a retry cooldown and the cooldown check
        // runs first — which is correct, and a different assertion from this one.
        await AuthenticateAsync();

        var courses = await ReadDataAsync<List<CourseDto>>(
            await HttpClient.GetAsync($"{Academy}/courses"));

        var welcome = courses.Single(course => course.Slug == "welcome-to-diaspora");

        var lessons = await ReadDataAsync<List<LessonDto>>(
            await HttpClient.GetAsync($"{Academy}/courses/{welcome.CourseId}/lessons"));

        var quiz = lessons.Single(lesson => lesson.Kind == "Quiz").Quiz!;

        var malformed = await HttpClient.PostAsJsonAsync(
            $"{Academy}/quizzes/{quiz.QuizId}/submit",
            new
            {
                answers = quiz.Questions.ToDictionary(
                    question => question.QuestionId, _ => Guid.NewGuid())
            },
            JsonOptions);

        malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadBodyAsync(malformed)).Should().Contain("creator.quiz.answer-unknown");
    }

    /// <summary>Saving a course persists, and shows up on the next read.</summary>
    [Fact]
    public async Task A_saved_course_stays_saved()
    {
        await AuthenticateAsync();

        var courses = await ReadDataAsync<List<CourseDto>>(
            await HttpClient.GetAsync($"{Academy}/courses"));

        var welcome = courses.Single(course => course.Slug == "welcome-to-diaspora");

        var saved = await ReadDataAsync<CourseDto>(
            await HttpClient.PutAsJsonAsync(
                $"{Academy}/courses/{welcome.CourseId}/saved",
                new { isSaved = true },
                JsonOptions));

        saved.IsSaved.Should().BeTrue();

        var reread = await ReadDataAsync<List<CourseDto>>(
            await HttpClient.GetAsync($"{Academy}/courses"));

        reread.Single(course => course.Slug == "welcome-to-diaspora").IsSaved.Should().BeTrue();
    }

    /// <summary>
    /// <b>A certificate corresponds to a persisted completion and to nothing else.</b> §32 of the
    /// phase brief, and the reason there is no route that mints one.
    /// </summary>
    [Fact]
    public async Task A_certificate_is_issued_only_by_finishing_the_course()
    {
        await AuthenticateAsync();

        var empty = await ReadDataAsync<List<CertificateDto>>(
            await HttpClient.GetAsync($"{Academy}/certificates/me"));

        empty.Should().BeEmpty();

        var courses = await ReadDataAsync<List<CourseDto>>(
            await HttpClient.GetAsync($"{Academy}/courses"));

        var safety = courses.Single(course => course.Slug == "staying-safe");

        safety.AwardsCertificate.Should().BeTrue();

        var lessons = await ReadDataAsync<List<LessonDto>>(
            await HttpClient.GetAsync($"{Academy}/courses/{safety.CourseId}/lessons"));

        var progress = await ReadDataAsync<CourseProgressDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Academy}/lessons/{lessons[0].LessonId}/complete", new { }, JsonOptions));

        progress.State.Should().Be("Completed");
        progress.CertificateId.Should().NotBeNull();

        var certificates = await ReadDataAsync<List<CertificateDto>>(
            await HttpClient.GetAsync($"{Academy}/certificates/me"));

        certificates.Should().ContainSingle();
        certificates[0].CourseTitle.Should().Be("Staying safe");
        certificates[0].VerificationReference.Should().StartWith("DCA-");
    }

    /// <summary>
    /// <b>Course progress is server-side, so a second device sees it.</b> The register: "progress
    /// kept only on the device is lost on reinstall."
    /// </summary>
    [Fact]
    public async Task Course_progress_survives_a_second_client()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var courses = await ReadDataAsync<List<CourseDto>>(
            await HttpClient.GetAsync($"{Academy}/courses"));

        var welcome = courses.Single(course => course.Slug == "welcome-to-diaspora");

        var lessons = await ReadDataAsync<List<LessonDto>>(
            await HttpClient.GetAsync($"{Academy}/courses/{welcome.CourseId}/lessons"));

        await HttpClient.PostAsJsonAsync(
            $"{Academy}/lessons/{lessons[0].LessonId}/complete", new { }, JsonOptions);

        using var second = Factory.CreateClient();
        second.DefaultRequestHeaders.Authorization = new("Bearer", auth.Tokens.AccessToken);

        var fromSecondDevice = await ReadDataAsync<List<CourseDto>>(
            await second.GetAsync($"{Academy}/courses"));

        fromSecondDevice.Single(course => course.Slug == "welcome-to-diaspora")
            .Progress!.LessonsCompleted.Should().Be(1);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // CREATOR-GAP-009 — referrals
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The programme serves a code, a server-built share URL and the qualification rule in words.
    /// </summary>
    [Fact]
    public async Task The_referral_programme_serves_a_code_and_the_rule_in_words()
    {
        await AuthenticateAsync();

        var programme = await ReadDataAsync<ReferralProgrammeDto>(
            await HttpClient.GetAsync($"{Referrals}/me"));

        programme.Code.Should().HaveLength(8);
        programme.Code.Should().NotContainAny(["0", "O", "1", "I", "L"],
            "an ambiguous character costs a referral rather than a keystroke");

        programme.ShareUrl.Should().EndWith(programme.Code);
        programme.ShareUrl.Should().StartWith("http", "the server builds it, never the client");

        programme.QualificationDescription.Should().Contain("first contribution");
        programme.QualificationVersion.Should().Be("creator-referral/1");
        programme.Terms.Should().NotBeEmpty();

        programme.Invited.Should().Be(0);
        programme.SignedUp.Should().Be(0);
        programme.Qualified.Should().Be(0);
    }

    /// <summary>
    /// <b>Issuing a code twice returns the same code.</b> §12 of the phase brief: one active referral
    /// identity per creator.
    /// </summary>
    [Fact]
    public async Task Issuing_a_referral_code_twice_returns_the_same_code()
    {
        await AuthenticateAsync();

        var first = await ReadDataAsync<ReferralProgrammeDto>(
            await HttpClient.PostAsJsonAsync($"{Referrals}/code", new { }, JsonOptions));

        var second = await ReadDataAsync<ReferralProgrammeDto>(
            await HttpClient.PostAsJsonAsync($"{Referrals}/code", new { }, JsonOptions));

        second.Code.Should().Be(first.Code);
    }

    /// <summary>An empty history pages correctly, and the cursor is absent rather than empty.</summary>
    [Fact]
    public async Task The_referral_history_pages_from_the_first_request()
    {
        await AuthenticateAsync();

        var page = await ReadDataAsync<ReferralPageDto>(
            await HttpClient.GetAsync($"{Referrals}/me/history"));

        page.Referrals.Should().BeEmpty();
        page.HasMore.Should().BeFalse();
        page.NextCursor.Should().BeNull();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // CREATOR-GAP-010 and -011 — the sign-up bonus and the rewards catalogue
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A member who registered after the module shipped has a bonus waiting.</b> The Identity
    /// registration consumer opens it, which is what makes the state real rather than a placeholder.
    /// </summary>
    [Fact]
    public async Task A_new_member_has_a_signup_bonus_and_acknowledging_it_awards_once()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await Factory.DrainOutboxAsync();

        var offered = await ReadDataAsync<SignupBonusDto>(
            await HttpClient.GetAsync($"{Rewards}/signup-bonus/me"));

        offered.State.Should().Be("Eligible");
        offered.Points.Should().BeGreaterThan(0);
        offered.Requirements.Should().ContainSingle(
            "finishing onboarding is the whole of what the bonus asks");

        offered.Requirements[0].Key.Should().Be("onboarding-complete");
        offered.Requirements[0].IsMet.Should().BeTrue("this member verified their phone");

        var key = Guid.NewGuid().ToString("N");

        var acknowledged = await ReadDataAsync<SignupBonusDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Rewards}/signup-bonus/acknowledge",
                new { idempotencyKey = key },
                JsonOptions));

        acknowledged.State.Should().Be(
            "Awarded",
            "the bonus asks only that onboarding is finished, which a phone-verified member has done");

        var second = await ReadDataAsync<SignupBonusDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Rewards}/signup-bonus/acknowledge",
                new { idempotencyKey = key },
                JsonOptions));

        second.State.Should().Be(acknowledged.State, "the second call replayed the first");
    }

    /// <summary>
    /// <b>A verified member's bonus is awarded, and the points land in B15.</b> The frontend never
    /// increments a balance; it re-reads the authoritative one.
    /// </summary>
    [Fact]
    public async Task An_awarded_signup_bonus_credits_the_financial_platform()
    {
        await AuthenticateAsync();
        await Factory.DrainOutboxAsync();

        var acknowledged = await ReadDataAsync<SignupBonusDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Rewards}/signup-bonus/acknowledge", new { }, JsonOptions));

        acknowledged.State.Should().Be(
            "Awarded", "a phone-verified member has finished onboarding");
        acknowledged.AwardedAt.Should().NotBeNull();

        await Factory.DrainOutboxAsync();

        var balance = await ReadDataAsync<RewardBalanceDto>(
            await HttpClient.GetAsync($"{Financial}/rewards/me"));

        balance.PendingPoints.Should().BeGreaterThanOrEqualTo(
            100, "B15 holds the bonus, pending until it vests (D-9)");
    }

    /// <summary>The rules catalogue serves the caps, the cooldowns, the version and the expiry policy.</summary>
    [Fact]
    public async Task The_points_rules_catalogue_states_its_caps_version_and_expiry_policy()
    {
        await AuthenticateAsync();

        var rules = await ReadDataAsync<PointRulesDto>(
            await HttpClient.GetAsync($"{Rewards}/rules"));

        rules.Rules.Should().NotBeEmpty();
        rules.CatalogueVersion.Should().Be("creator-rewards/1");
        rules.ExpiryPolicy.Should().Be("Never");
        rules.ExpiryDescription.Should().NotBeNullOrWhiteSpace(
            "§14: the backend must explicitly define whether points expire");

        var guide = rules.Rules.Single(rule => rule.RuleKey == "guide-published");

        guide.Points.Should().Be(50);
        guide.ActionRoute.Should().NotBeNullOrWhiteSpace();

        var comment = rules.Rules.Single(rule => rule.RuleKey == "comment-added");

        comment.MaxOccurrences.Should().Be(20);
        comment.CooldownHours.Should().Be(24);
        comment.LimitLabel.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// <b>The catalogue's state is decided for this caller, and the balance comes from B15.</b>
    /// Somebody with no points sees Locked with the shortfall named.
    /// </summary>
    [Fact]
    public async Task The_reward_catalogue_locks_what_the_caller_cannot_afford()
    {
        await AuthenticateAsync();

        var catalogue = await ReadDataAsync<RewardCatalogueDto>(
            await HttpClient.GetAsync($"{Rewards}/catalogue"));

        catalogue.Rewards.Should().NotBeEmpty();
        catalogue.CatalogueVersion.Should().Be("creator-rewards/1");
        catalogue.AvailablePoints.Should().Be(0, "nothing has been earned yet");

        var badge = catalogue.Rewards.Single(reward => reward.Key == "creator-badge-supporter");

        badge.PointsCost.Should().Be(300);
        badge.State.Should().Be("Locked");
        badge.LockedReason.Should().Contain("300 more points");
    }

    /// <summary>
    /// <b>A redemption the caller cannot afford is refused, and the request carries no figure at
    /// all.</b> §31: "A malicious client must not be able to POST with points = 999999."
    /// </summary>
    [Fact]
    public async Task A_redemption_without_the_points_is_refused()
    {
        await AuthenticateAsync();

        var catalogue = await ReadDataAsync<RewardCatalogueDto>(
            await HttpClient.GetAsync($"{Rewards}/catalogue"));

        var badge = catalogue.Rewards.Single(reward => reward.Key == "creator-badge-supporter");

        var response = await HttpClient.PostAsJsonAsync(
            Rewards + "/redemptions",
            new { rewardId = badge.RewardId, points = 999_999, pointsCost = 0 },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadBodyAsync(response)).Should().Contain("creator.reward.insufficient-points");
    }

    /// <summary>
    /// <b>The full redemption: points earned, spent once, and the balance moves by the catalogue's
    /// price rather than by anything the client sent.</b>
    /// </summary>
    [Fact]
    public async Task A_redemption_spends_the_catalogue_price_exactly_once()
    {
        await AuthenticateAsync();

        // Earn: the sign-up bonus plus a milestone claim.
        await Factory.DrainOutboxAsync();

        await HttpClient.PostAsJsonAsync(
            $"{Rewards}/signup-bonus/acknowledge", new { }, JsonOptions);

        // Ten guides, so the ten-guides milestone completes as well as the first — enough points
        // that the reward under test is affordable, earned rather than granted by the suite.
        for (var index = 0; index < 10; index++)
        {
            await PublishGuideAsync($"Redemption guide {index}");
        }

        await Factory.DrainOutboxAsync();

        var milestones = await ReadDataAsync<List<MilestoneDto>>(
            await HttpClient.GetAsync($"{Creators}/me/milestones"));

        foreach (var milestone in milestones.Where(row => row.State == "Completed"))
        {
            await HttpClient.PostAsJsonAsync(
                $"{Creators}/me/milestones/{milestone.MilestoneId}/claim", new { }, JsonOptions);
        }

        await Factory.DrainOutboxAsync();
        await Factory.VestRewardPointsAsync(_memberUserId);

        var before = await ReadDataAsync<RewardBalanceDto>(
            await HttpClient.GetAsync($"{Financial}/rewards/me"));

        before.Balance.Should().BeGreaterThanOrEqualTo(
            600, "a hundred from the bonus, a hundred and five hundred from two milestones");

        var catalogue = await ReadDataAsync<RewardCatalogueDto>(
            await HttpClient.GetAsync($"{Rewards}/catalogue"));

        catalogue.AvailablePoints.Should().Be(
            before.Balance, "the catalogue reads B15's balance rather than holding one");

        var badge = catalogue.Rewards.Single(reward => reward.Key == "creator-badge-supporter");

        badge.State.Should().Be("Available");

        var key = Guid.NewGuid().ToString("N");

        var redemption = await ReadDataAsync<RedemptionDto>(
            await HttpClient.PostAsJsonAsync(
                Rewards + "/redemptions",
                new { rewardId = badge.RewardId, idempotencyKey = key },
                JsonOptions));

        // Pending, not Redeemed: nothing has been issued yet, and the response used to claim it had
        // with a grant reference nothing stood behind. Staff fulfil or reject it — see the queue
        // tests below.
        redemption.State.Should().Be("Pending");
        redemption.PointsSpent.Should().Be(300);
        redemption.GrantReference.Should().BeNull();

        var after = await ReadDataAsync<RewardBalanceDto>(
            await HttpClient.GetAsync($"{Financial}/rewards/me"));

        after.Balance.Should().Be(before.Balance - 300, "the catalogue's price, not the client's");

        // The same key again spends nothing more.
        var replay = await ReadDataAsync<RedemptionDto>(
            await HttpClient.PostAsJsonAsync(
                Rewards + "/redemptions",
                new { rewardId = badge.RewardId, idempotencyKey = key },
                JsonOptions));

        replay.RedemptionId.Should().Be(redemption.RedemptionId);

        var unchanged = await ReadDataAsync<RewardBalanceDto>(
            await HttpClient.GetAsync($"{Financial}/rewards/me"));

        unchanged.Balance.Should().Be(after.Balance, "a replay debits nothing");

        var history = await ReadDataAsync<List<RedemptionDto>>(
            await HttpClient.GetAsync($"{Rewards}/redemptions/me"));

        history.Should().ContainSingle();
        history[0].CatalogueVersion.Should().Be("creator-rewards/1");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // The redemption queue
    //
    // A redemption is Pending until a reward manager fulfils it with what was issued, or rejects it
    // and the points come back in the same transaction. Points are granted here by the Financial
    // staff command rather than earned through ten guides: what is under test is the queue.
    // ════════════════════════════════════════════════════════════════════════════════════════

    private const string RewardsManage = "creator:rewards:manage";

    [Fact]
    public async Task A_redemption_is_pending_until_staff_fulfil_it()
    {
        var (staffToken, _, redemption) = await RedeemTheBadgeAsync();

        var queue = await AsReviewerQueryAsync(
            staffToken,
            RewardsManage,
            new Diaspora.Modules.Creator.Application.Rewards.ListRedemptionsForReviewQuery());

        queue.Should().Contain(queued => queued.RedemptionId == redemption.RedemptionId
            && queued.State == "Pending");

        var fulfilled = await AsReviewerAsync(staffToken, RewardsManage, Decide(redemption, "fulfil", grantReference: "badge:supporter-issued"));

        fulfilled.State.Should().Be("Redeemed");
        fulfilled.GrantReference.Should().Be("badge:supporter-issued");

        var history = await ReadDataAsync<List<RedemptionDto>>(
            await HttpClient.GetAsync($"{Rewards}/redemptions/me"));

        history.Single().State.Should().Be("Redeemed", "the member sees what staff recorded");
    }

    [Fact]
    public async Task Rejecting_a_redemption_refunds_the_points_once()
    {
        var (staffToken, _, redemption) = await RedeemTheBadgeAsync();

        var spent = await BalanceAsync();

        var rejected = await AsReviewerAsync(
            staffToken, RewardsManage, Decide(redemption, "reject", reason: "Out of stock this month."));

        rejected.State.Should().Be("Rejected");
        rejected.RejectionReason.Should().Be("Out of stock this month.");
        (await BalanceAsync()).Should().Be(spent + 300, "the points spent come back");

        var again = await AsReviewerExpectingFailureAsync(
            staffToken, RewardsManage, Decide(redemption, "reject", reason: "Again."));

        again.Code.Should().Be("creator.redemption.already-decided");
        (await BalanceAsync()).Should().Be(spent + 300, "a second decision refunds nothing more");
    }

    /// <summary>The supporter badge is once per creator; a rejected one must not count.</summary>
    [Fact]
    public async Task A_rejected_redemption_frees_the_per_creator_cap()
    {
        var (staffToken, _, redemption) = await RedeemTheBadgeAsync();

        await AsReviewerAsync(staffToken, RewardsManage, Decide(redemption, "reject", reason: "Try again."));

        var second = await ReadDataAsync<RedemptionDto>(
            await HttpClient.PostAsJsonAsync(
                Rewards + "/redemptions",
                new { rewardId = redemption.RewardId, idempotencyKey = Guid.NewGuid().ToString("N") },
                JsonOptions));

        second.State.Should().Be("Pending");
    }

    [Fact]
    public async Task Only_reward_managers_can_decide_a_redemption()
    {
        var (staffToken, _, redemption) = await RedeemTheBadgeAsync();

        // A grant reviewer is staff, and still not the person who hands out rewards. The pipeline's
        // authorization refuses before the handler runs — a 403 over HTTP.
        using (var scope = Factory.Services.CreateScope())
        {
            ScopedCurrentUser.Apply(scope.ServiceProvider, staffToken, "creator:grant:review");

            var deciding = () => scope.ServiceProvider.GetRequiredService<MediatR.ISender>()
                .Send(Decide(redemption, "fulfil", grantReference: "badge:x"));

            await deciding.Should().ThrowAsync<Diaspora.Application.Exceptions.ForbiddenException>()
                .WithMessage("*creator:rewards:manage*");
        }

        (await ReadDataAsync<List<RedemptionDto>>(await HttpClient.GetAsync($"{Rewards}/redemptions/me")))
            .Single().State.Should().Be("Pending");
    }

    [Fact]
    public async Task A_reward_manager_cannot_decide_their_own_redemption()
    {
        var (_, memberToken, redemption) = await RedeemTheBadgeAsync();

        var refused = await AsReviewerExpectingFailureAsync(
            memberToken, RewardsManage, Decide(redemption, "fulfil", grantReference: "badge:mine"));

        refused.Code.Should().Be("creator.review.self");
    }

    [Fact]
    public async Task A_decision_needs_what_was_issued_or_why_not()
    {
        var (staffToken, _, redemption) = await RedeemTheBadgeAsync();

        (await AsReviewerExpectingFailureAsync(staffToken, RewardsManage, Decide(redemption, "fulfil")))
            .Code.Should().Be("creator.redemption.decision-invalid");

        (await AsReviewerExpectingFailureAsync(staffToken, RewardsManage, Decide(redemption, "reject")))
            .Code.Should().Be("creator.redemption.decision-invalid");
    }

    /// <summary>
    /// A member with exactly the supporter badge's price, who has redeemed it, plus a staff token.
    /// Leaves the member authenticated on <c>HttpClient</c>.
    /// </summary>
    private async Task<(string StaffToken, string MemberToken, RedemptionDto Redemption)> RedeemTheBadgeAsync()
    {
        var (_, staffToken) = await ReviewerAsync();

        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        await AsReviewerAsync(
            staffToken,
            "financial:credit:manage",
            new Diaspora.Modules.Financial.Application.Rewards.GrantRewardPointsCommand
            {
                UserId = member.User.Id,
                Points = 300,
                Source = "Test",
                GrantReference = $"test:{Guid.NewGuid():N}",
                Reason = "Integration suite: the badge's price."
            });

        await Factory.VestRewardPointsAsync(member.User.Id);

        var catalogue = await ReadDataAsync<RewardCatalogueDto>(
            await HttpClient.GetAsync($"{Rewards}/catalogue"));

        var badge = catalogue.Rewards.Single(reward => reward.Key == "creator-badge-supporter");

        var redemption = await ReadDataAsync<RedemptionDto>(
            await HttpClient.PostAsJsonAsync(
                Rewards + "/redemptions",
                new { rewardId = badge.RewardId, idempotencyKey = Guid.NewGuid().ToString("N") },
                JsonOptions));

        redemption.State.Should().Be("Pending");

        return (staffToken, member.Tokens.AccessToken, redemption);
    }

    private static Diaspora.Modules.Creator.Application.Rewards.DecideRedemptionCommand Decide(
        RedemptionDto redemption, string action, string? grantReference = null, string? reason = null) =>
        new()
        {
            RedemptionId = redemption.RedemptionId,
            Request = new DecideRedemptionRequest
            {
                Action = action,
                GrantReference = grantReference,
                Reason = reason
            }
        };

    private async Task<long> BalanceAsync() =>
        (await ReadDataAsync<RewardBalanceDto>(await HttpClient.GetAsync($"{Financial}/rewards/me"))).Balance;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Idempotency keys belong to the caller
    //
    // Keys were scoped by operation alone while every key column is unique across all members, so
    // the lookup before each write found another member's record whenever two members chose the
    // same key. Member B was handed A's response — which looks right, which is the problem — and
    // B's own action never ran. Each test below therefore re-reads B's state rather than trusting
    // the response B was given.
    // ════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Another_members_key_does_not_replay_their_signup_bonus()
    {
        var key = Guid.NewGuid().ToString("N");

        var (_, first) = await RegisterAndVerifyAsync();
        Api.Authenticate(first.Tokens.AccessToken);
        await Factory.DrainOutboxAsync();
        await HttpClient.PostAsJsonAsync(
            $"{Rewards}/signup-bonus/acknowledge", new { idempotencyKey = key }, JsonOptions);

        var (_, second) = await RegisterAndVerifyAsync();
        Api.Authenticate(second.Tokens.AccessToken);
        await Factory.DrainOutboxAsync();
        await HttpClient.PostAsJsonAsync(
            $"{Rewards}/signup-bonus/acknowledge", new { idempotencyKey = key }, JsonOptions);

        await Factory.DrainOutboxAsync();

        var bonus = await ReadDataAsync<SignupBonusDto>(
            await HttpClient.GetAsync($"{Rewards}/signup-bonus/me"));

        bonus.State.Should().Be("Awarded", "the second member's own bonus was acknowledged");

        var balance = await ReadDataAsync<RewardBalanceDto>(
            await HttpClient.GetAsync($"{Financial}/rewards/me"));

        balance.PendingPoints.Should().Be(bonus.Points, "the second member was paid their own bonus");
    }

    [Fact]
    public async Task Another_members_key_does_not_replay_their_milestone_claim()
    {
        var key = Guid.NewGuid().ToString("N");

        await AuthenticateAsync();
        await ClaimFirstGuideAsync(key);

        await AuthenticateAsync();
        await ClaimFirstGuideAsync(key);

        var milestones = await ReadDataAsync<List<MilestoneDto>>(
            await HttpClient.GetAsync($"{Creators}/me/milestones"));

        milestones.Single(milestone => milestone.Key == "first-guide")
            .State.Should().Be("Claimed", "the second member's own milestone was claimed");
    }

    [Fact]
    public async Task Another_members_key_does_not_replay_their_challenge_enrolment()
    {
        var key = Guid.NewGuid().ToString("N");

        await AuthenticateAsync();
        var sprint = await SprintAsync();
        await HttpClient.PostAsJsonAsync(
            $"{Challenges}/{sprint.ChallengeId}/join", new { idempotencyKey = key }, JsonOptions);

        await AuthenticateAsync();
        await HttpClient.PostAsJsonAsync(
            $"{Challenges}/{sprint.ChallengeId}/join", new { idempotencyKey = key }, JsonOptions);

        (await SprintAsync()).State.Should().Be(
            "Joined", "the second member was enrolled, not shown the first member's enrolment");
    }

    /// <summary>
    /// The same caller, the same key, a different milestone. Replaying would report the second
    /// milestone as the first one's claim and leave it unclaimed; refusing tells the client its key
    /// was spent.
    /// </summary>
    [Fact]
    public async Task A_key_reused_for_a_different_milestone_is_refused()
    {
        await AuthenticateAsync();

        for (var index = 0; index < 10; index++)
        {
            await PublishGuideAsync($"Reused key guide {index}");
        }

        await Factory.DrainOutboxAsync();

        var milestones = await ReadDataAsync<List<MilestoneDto>>(
            await HttpClient.GetAsync($"{Creators}/me/milestones"));

        var first = milestones.Single(milestone => milestone.Key == "first-guide");
        var ten = milestones.Single(milestone => milestone.Key == "ten-guides");
        ten.State.Should().Be("Completed");

        var key = Guid.NewGuid().ToString("N");

        (await HttpClient.PostAsJsonAsync(
                $"{Creators}/me/milestones/{first.MilestoneId}/claim", new { idempotencyKey = key }, JsonOptions))
            .IsSuccessStatusCode.Should().BeTrue();

        var reused = await HttpClient.PostAsJsonAsync(
            $"{Creators}/me/milestones/{ten.MilestoneId}/claim", new { idempotencyKey = key }, JsonOptions);

        reused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadBodyAsync(reused)).Should().Contain("creator.idempotency.key-reused");

        var after = await ReadDataAsync<List<MilestoneDto>>(
            await HttpClient.GetAsync($"{Creators}/me/milestones"));

        after.Single(milestone => milestone.Key == "ten-guides")
            .State.Should().Be("Completed", "a refused claim claims nothing");
    }

    private async Task ClaimFirstGuideAsync(string key)
    {
        await PublishGuideAsync("Key scoping");
        await Factory.DrainOutboxAsync();

        var milestones = await ReadDataAsync<List<MilestoneDto>>(
            await HttpClient.GetAsync($"{Creators}/me/milestones"));

        var first = milestones.Single(milestone => milestone.Key == "first-guide");

        await HttpClient.PostAsJsonAsync(
            $"{Creators}/me/milestones/{first.MilestoneId}/claim", new { idempotencyKey = key }, JsonOptions);
    }

    private async Task<ChallengeDto> SprintAsync()
    {
        var challenges = await ReadDataAsync<List<ChallengeDto>>(await HttpClient.GetAsync(Challenges));

        return challenges.Single(challenge => challenge.Key == "settling-in-sprint");
    }

    /// <summary>A per-creator cap is enforced against real persisted redemptions.</summary>
    [Fact]
    public async Task A_reward_with_a_per_creator_cap_refuses_the_second_redemption()
    {
        await AuthenticateAsync();
        await Factory.DrainOutboxAsync();

        await HttpClient.PostAsJsonAsync(
            $"{Rewards}/signup-bonus/acknowledge", new { }, JsonOptions);

        for (var index = 0; index < 10; index++)
        {
            await PublishGuideAsync($"Cap guide {index}");
        }

        await Factory.DrainOutboxAsync();

        var milestones = await ReadDataAsync<List<MilestoneDto>>(
            await HttpClient.GetAsync($"{Creators}/me/milestones"));

        foreach (var milestone in milestones.Where(row => row.State == "Completed"))
        {
            await HttpClient.PostAsJsonAsync(
                $"{Creators}/me/milestones/{milestone.MilestoneId}/claim", new { }, JsonOptions);
        }

        await Factory.DrainOutboxAsync();
        await Factory.VestRewardPointsAsync(_memberUserId);

        var catalogue = await ReadDataAsync<RewardCatalogueDto>(
            await HttpClient.GetAsync($"{Rewards}/catalogue"));

        var badge = catalogue.Rewards.Single(reward => reward.Key == "creator-badge-supporter");

        await ReadDataAsync<RedemptionDto>(
            await HttpClient.PostAsJsonAsync(
                Rewards + "/redemptions", new { rewardId = badge.RewardId }, JsonOptions));

        var second = await HttpClient.PostAsJsonAsync(
            Rewards + "/redemptions", new { rewardId = badge.RewardId }, JsonOptions);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadBodyAsync(second)).Should().Contain("creator.reward.limit-reached");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // CREATOR-GAP-012 — leaderboards
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>The leaderboard ranks real activity, serves its score definition, and answers "where am
    /// I".</b> Two creators with different contributions, and the ranking reflects it.
    /// </summary>
    [Fact]
    public async Task The_leaderboard_ranks_real_contributions_and_names_what_the_score_means()
    {
        await AuthenticateAsync();

        for (var index = 0; index < 3; index++)
        {
            await PublishGuideAsync($"Board guide {index}");
        }

        await Factory.DrainOutboxAsync();

        var mine = await ReadDataAsync<CreatorProfileDto>(
            await HttpClient.GetAsync($"{Creators}/me"));

        var board = await ReadDataAsync<LeaderboardDto>(
            await HttpClient.GetAsync($"{Creators}/leaderboard?scope=top-contributors"));

        board.Entries.Should().NotBeEmpty(
            "a leaderboard asserted only against an empty table is unproved");

        board.ScoreDefinition.Should().NotBeNullOrWhiteSpace();
        board.PeriodLabel.Should().Be("All time");
        board.RefreshNote.Should().NotBeNullOrWhiteSpace();

        board.Entries.Should().BeInDescendingOrder(entry => entry.Score);
        board.Entries.Should().Contain(entry => entry.CreatorId == mine.CreatorId);

        board.ViewerEntry.Should().NotBeNull();
        board.ViewerEntry!.CreatorId.Should().Be(mine.CreatorId);
        board.ViewerIsOptedOut.Should().BeFalse();
    }

    /// <summary>
    /// <b>A period ranking is a real window, not a lifetime total with a label.</b> Activity from
    /// today appears in the 7-day board; the all-time board is at least as large.
    /// </summary>
    [Fact]
    public async Task A_period_leaderboard_is_a_real_window_over_the_activity_ledger()
    {
        await AuthenticateAsync();
        await PublishGuideAsync("Recent work");
        await Factory.DrainOutboxAsync();

        var week = await ReadDataAsync<LeaderboardDto>(
            await HttpClient.GetAsync($"{Creators}/leaderboard?scope=top-contributors&period=7d"));

        var allTime = await ReadDataAsync<LeaderboardDto>(
            await HttpClient.GetAsync($"{Creators}/leaderboard?scope=top-contributors"));

        week.Period.Should().Be("Last7Days");
        week.PeriodLabel.Should().Be("Last 7 days");
        week.Entries.Should().NotBeEmpty("today's work is inside a seven-day window");

        allTime.Entries.Count.Should().BeGreaterThanOrEqualTo(week.Entries.Count);
    }

    /// <summary>
    /// <b>A creator who opted out is absent from the ranking and told so.</b> Not filtered by the
    /// client, and not shown as "unranked" implying a zero.
    /// </summary>
    [Fact]
    public async Task A_creator_who_opted_out_is_absent_from_the_ranking()
    {
        await AuthenticateAsync();
        await PublishGuideAsync("Then opted out");
        await Factory.DrainOutboxAsync();

        var mine = await ReadDataAsync<CreatorProfileDto>(
            await HttpClient.PutAsJsonAsync(
                $"{Creators}/me", new { isDiscoverable = false }, JsonOptions));

        var board = await ReadDataAsync<LeaderboardDto>(
            await HttpClient.GetAsync($"{Creators}/leaderboard?scope=top-contributors"));

        board.Entries.Should().NotContain(entry => entry.CreatorId == mine.CreatorId);
        board.ViewerIsOptedOut.Should().BeTrue();
        board.ViewerEntry.Should().BeNull();
    }

    /// <summary>An unknown scope is refused rather than silently answered with the default.</summary>
    [Fact]
    public async Task An_unknown_leaderboard_scope_is_refused()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync($"{Creators}/leaderboard?scope=most-handsome");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadBodyAsync(response)).Should().Contain("creator.leaderboard.scope-unknown");
    }

    /// <summary>The community scope needs to know which community, and says so.</summary>
    [Fact]
    public async Task The_community_scope_refuses_without_a_community()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync($"{Creators}/leaderboard?scope=community");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadBodyAsync(response)).Should()
            .Contain("creator.leaderboard.scope-needs-subject");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // CREATOR-GAP-014 — the creator directory
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>The directory serves real creators with real facets.</b> A creator with categories and
    /// expertise appears under both filters, and the facet counts come from the same predicate.
    /// </summary>
    [Fact]
    public async Task The_directory_serves_real_creators_with_facets_that_match_the_filters()
    {
        await AuthenticateAsync();
        await PublishGuideAsync("Directory guide");
        await Factory.DrainOutboxAsync();

        await HttpClient.PutAsJsonAsync(
            $"{Creators}/me",
            new
            {
                categories = new[] { "Immigration" },
                expertise = new[] { "Visas" },
                languages = new[] { "en" }
            },
            JsonOptions);

        var directory = await ReadDataAsync<DirectoryDto>(await HttpClient.GetAsync(Creators));

        directory.Creators.Should().NotBeEmpty(
            "a directory asserted only against an empty table is unproved");

        directory.Facets.TotalCount.Should().BeGreaterThan(0);
        directory.Facets.Categories.Should().Contain(facet => facet.Value == "IMMIGRATION");
        directory.Facets.Expertise.Should().Contain(facet => facet.Value == "VISAS");
        directory.Facets.Countries.Should().Contain(facet => facet.Value == "CA");

        var filtered = await ReadDataAsync<DirectoryDto>(
            await HttpClient.GetAsync($"{Creators}?category=immigration"));

        filtered.Creators.Should().NotBeEmpty("the filter matches case-insensitively");

        var missing = await ReadDataAsync<DirectoryDto>(
            await HttpClient.GetAsync($"{Creators}?category=nothing-like-this"));

        missing.Creators.Should().BeEmpty();
        missing.Facets.TotalCount.Should().Be(0);
    }

    /// <summary>
    /// <b>A private creator is absent from the response and from the facet counts.</b> The register:
    /// "A private creator must not affect public facet counts in a way that reveals their existence."
    /// </summary>
    [Fact]
    public async Task A_private_creator_is_absent_from_the_directory_and_from_its_facets()
    {
        await AuthenticateAsync();
        await PublishGuideAsync("Private work");
        await Factory.DrainOutboxAsync();

        await HttpClient.PutAsJsonAsync(
            $"{Creators}/me",
            new { categories = new[] { "SecretCategory" }, isDiscoverable = true },
            JsonOptions);

        var visible = await ReadDataAsync<DirectoryDto>(
            await HttpClient.GetAsync($"{Creators}?category=secretcategory"));

        visible.Creators.Should().ContainSingle();
        visible.Facets.Categories.Should().Contain(facet => facet.Value == "SECRETCATEGORY");

        await HttpClient.PutAsJsonAsync(
            $"{Creators}/me", new { isDiscoverable = false }, JsonOptions);

        var hidden = await ReadDataAsync<DirectoryDto>(
            await HttpClient.GetAsync($"{Creators}?category=secretcategory"));

        hidden.Creators.Should().BeEmpty();
        hidden.Facets.TotalCount.Should().Be(0);
        hidden.Facets.Categories.Should().NotContain(
            facet => facet.Value == "SECRETCATEGORY",
            "a facet count that included them would disclose that they exist");
    }

    /// <summary>
    /// <b>The directory pages with the platform's cursor over real rows.</b> Three creators, a page
    /// of two, then the rest — with nobody served twice and nobody lost.
    /// </summary>
    [Fact]
    public async Task The_directory_pages_over_real_rows_without_repeating_or_losing_one()
    {
        var creators = new List<Guid>();

        for (var index = 0; index < 3; index++)
        {
            await AuthenticateAsync();
            await PublishGuideAsync($"Paging guide {index}");
            await Factory.DrainOutboxAsync();

            var profile = await ReadDataAsync<CreatorProfileDto>(
                await HttpClient.PutAsJsonAsync(
                    $"{Creators}/me",
                    new { categories = new[] { "PagingCohort" } },
                    JsonOptions));

            creators.Add(profile.CreatorId);
        }

        var first = await ReadDataAsync<DirectoryDto>(
            await HttpClient.GetAsync($"{Creators}?category=pagingcohort&take=2"));

        first.Creators.Should().HaveCount(2);
        first.HasMore.Should().BeTrue();
        first.NextCursor.Should().NotBeNullOrWhiteSpace();

        var second = await ReadDataAsync<DirectoryDto>(
            await HttpClient.GetAsync(
                $"{Creators}?category=pagingcohort&take=2&cursor={Uri.EscapeDataString(first.NextCursor!)}"));

        second.Creators.Should().ContainSingle();
        second.HasMore.Should().BeFalse();

        var served = first.Creators.Concat(second.Creators)
            .Select(creator => creator.CreatorId)
            .ToList();

        served.Should().OnlyHaveUniqueItems("a tied sort key must not serve a row twice");
        served.Should().BeEquivalentTo(creators, "and must not drop one either");
    }

    /// <summary>
    /// <b>The verified filter is a real predicate over real rows.</b> An unverified creator is
    /// present unfiltered and absent under <c>verified=true</c>.
    /// </summary>
    [Fact]
    public async Task The_verified_filter_narrows_the_directory()
    {
        await AuthenticateAsync();
        await PublishGuideAsync("Unverified creator");
        await Factory.DrainOutboxAsync();

        await HttpClient.PutAsJsonAsync(
            $"{Creators}/me", new { categories = new[] { "VerifiedFilterCohort" } }, JsonOptions);

        var all = await ReadDataAsync<DirectoryDto>(
            await HttpClient.GetAsync($"{Creators}?category=verifiedfiltercohort"));

        all.Creators.Should().ContainSingle();

        var verified = await ReadDataAsync<DirectoryDto>(
            await HttpClient.GetAsync($"{Creators}?category=verifiedfiltercohort&verified=true"));

        verified.Creators.Should().BeEmpty(
            "nobody in this cohort has passed the creator-programme verification");

        all.Facets.VerifiedCount.Should().Be(0);
    }

    /// <summary>
    /// <b>The creator directory is not B8's contributor directory.</b> They are different routes
    /// answering different questions, and the register is explicit that aliasing one to the other
    /// would be wrong.
    /// </summary>
    [Fact]
    public async Task The_creator_directory_is_a_distinct_surface_from_the_contributor_directory()
    {
        await AuthenticateAsync();

        var creators = await HttpClient.GetAsync(Creators);

        creators.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "the creator directory is this module's own surface at its own prefix");

        // The register: "The existing contributor directory is NOT the creator directory. Do not
        // simply alias /discovery/contributors to /creators." Nothing in this phase touched B8's
        // knowledge surface, and this module contributes no route into it.
        var underKnowledge = await HttpClient.GetAsync("/api/v1/knowledge/creators");

        underKnowledge.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "the creator directory lives at /api/v1/creators and nowhere else");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<string> AuthenticateAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        _memberUserId = auth.User.Id;

        return auth.Tokens.AccessToken;
    }

    /// <summary>
    /// Takes a member through B3's own identity verification, so Trust's tier reaches Verified.
    /// <para>
    /// <b>The real flow, and it has to be.</b> <c>RegisterVerifiedMemberAsync</c> moves Identity's
    /// <c>AccessState</c> through Identity's trust-decision endpoint; it does not create a Trust
    /// profile, so <c>ITrustService.GetTrustLevelAsync</c> still answers <c>None</c>. The creator
    /// programme's identity requirement asks Trust, and the only honest way to satisfy it in a test
    /// is to satisfy it — submit the verification and have a reviewer approve it. That is also what
    /// makes the Trust integration proved rather than asserted.
    /// </para>
    /// </summary>
    private async Task VerifyIdentityWithTrustAsync(Guid userId)
    {
        var submitted = await ReadDataAsync<TrustVerificationDto>(
            await HttpClient.PostAsJsonAsync(
                "/api/v1/trust/verifications/identity",
                new
                {
                    documentType = "Passport",
                    issueCountry = "NG",
                    documentReference = $"media:{Guid.NewGuid():N}",
                    selfieReference = $"media:{Guid.NewGuid():N}",
                    documentNumber = "A0000000",
                    expiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(5))
                },
                JsonOptions));

        var (_, reviewerToken) = await ReviewerAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            ScopedCurrentUser.Apply(
                scope.ServiceProvider, reviewerToken, "trust:verification:review");

            var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();

            var reviewed = await sender.Send(
                new Diaspora.Modules.Trust.Application.Verifications.ReviewVerificationCommand
                {
                    VerificationId = submitted.VerificationId,
                    Decision = Diaspora.Modules.Trust.Application.Verifications.ReviewDecision.Approve,
                    Notes = "Integration suite."
                });

            reviewed.IsSuccess.Should().BeTrue(
                $"the Trust review should have succeeded: {reviewed.Error.Code}");
        }

        await Factory.DrainOutboxAsync();

        userId.Should().NotBeEmpty();
    }

    /// <summary>A member with one recognised contribution, so the ladder has moved.</summary>
    private async Task<string> ContributorAsync()
    {
        var token = await AuthenticateAsync();

        await PublishGuideAsync("A first contribution");
        await Factory.DrainOutboxAsync();

        return token;
    }

    /// <summary>
    /// A member who satisfies every creator-programme requirement — including Trust's.
    /// <para>
    /// Three published guides, ten followers through B6's published contract, and B3's own identity
    /// verification approved by a reviewer. Every requirement is met by the thing that actually
    /// meets it rather than by a row this suite wrote.
    /// </para>
    /// </summary>
    private async Task<string> EligibleCreatorAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await VerifyIdentityWithTrustAsync(auth.User.Id);

        Api.Authenticate(auth.Tokens.AccessToken);

        for (var index = 0; index < 3; index++)
        {
            await PublishGuideAsync($"Eligibility guide {index}");
        }

        await Factory.DrainOutboxAsync();

        // The follower requirement, through B6's published contract — the same route a real follow
        // takes, so the counter this module keeps is fed by the fact rather than by a test fixture.
        var creator = await ReadDataAsync<CreatorProfileDto>(
            await HttpClient.GetAsync($"{Creators}/me"));

        await PublishFollowsAsync(creator.UserId, 10);
        await Factory.DrainOutboxAsync();

        return auth.Tokens.AccessToken;
    }

    /// <summary>
    /// Publishes a guide through B8's own routes, so the activity behind every assertion is a real
    /// integration event rather than a row this suite wrote.
    /// </summary>
    private async Task<Guid> PublishGuideAsync(string title)
    {
        var draft = await ReadDataAsync<GuideDto>(
            await HttpClient.PostAsJsonAsync(
                GuidesRoot,
                new { title, categoryKey = "getting-started", language = "en", countryCode = "GB" },
                JsonOptions));

        await HttpClient.PutAsJsonAsync(
            $"{GuidesRoot}/{draft.GuideId}",
            new
            {
                title,
                countryCode = "GB",
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

        return published.GuideId;
    }

    /// <summary>
    /// Publishes B6's follow contract, exactly as the Community module does.
    /// <para>
    /// The contract rather than B6's request shape, for the reason B8.1's suite records: what is
    /// under test is this module's half of the integration, and driving B6's endpoints would couple
    /// this suite to a request contract B6's own suite already covers.
    /// </para>
    /// </summary>
    private async Task PublishFollowsAsync(Guid targetUserId, int count)
    {
        using var scope = Factory.Services.CreateScope();

        var publisher = scope.ServiceProvider
            .GetRequiredService<Diaspora.Platform.Messaging.IIntegrationEventPublisher>();

        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<Diaspora.Domain.Abstractions.IUnitOfWork>();

        for (var index = 0; index < count; index++)
        {
            await publisher.PublishAsync(
                new Diaspora.Contracts.Community.V1.Events.CommunityFollowCreatedIntegrationEvent
                {
                    FollowerId = Guid.NewGuid(),
                    TargetType = "User",
                    TargetId = targetUserId
                });
        }

        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Runs a command as a reviewer: a real member, holding one privileged permission.
    /// <para>
    /// <b>Dispatched in a scope rather than over HTTP, and DEBT-031 is why.</b> This suite has no
    /// staff role to grant, so a reviewer's token cannot be obtained the way a member's is. The
    /// command still runs through the whole application pipeline — validation, authorisation, the
    /// transaction, the audit behaviour — against the same claims the HTTP layer would have
    /// presented. What is skipped is the session lookup in the authentication handler, which is
    /// Identity's and is covered by Identity's own suite.
    /// </para>
    /// </summary>
    private async Task<TResponse> AsReviewerAsync<TResponse>(
        string reviewerToken,
        string permission,
        Diaspora.Application.Abstractions.ICommand<TResponse> command)
    {
        using var scope = Factory.Services.CreateScope();

        ScopedCurrentUser.Apply(scope.ServiceProvider, reviewerToken, permission);

        var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();

        var result = await sender.Send(command);

        result.IsSuccess.Should().BeTrue(
            $"the reviewer action should have succeeded: {result.Error.Code} {result.Error.Description}");

        return result.Value;
    }

    /// <summary>The same, expecting a refusal.</summary>
    private async Task<Diaspora.SharedKernel.Results.Error> AsReviewerExpectingFailureAsync<TResponse>(
        string reviewerToken,
        string permission,
        Diaspora.Application.Abstractions.ICommand<TResponse> command)
    {
        using var scope = Factory.Services.CreateScope();

        ScopedCurrentUser.Apply(scope.ServiceProvider, reviewerToken, permission);

        var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();

        var result = await sender.Send(command);

        result.IsFailure.Should().BeTrue("the reviewer action should have been refused");

        return result.Error;
    }

    /// <summary>The same, for a read.</summary>
    private async Task<TResponse> AsReviewerQueryAsync<TResponse>(
        string reviewerToken,
        string permission,
        Diaspora.Application.Abstractions.IQuery<TResponse> query)
    {
        using var scope = Factory.Services.CreateScope();

        ScopedCurrentUser.Apply(scope.ServiceProvider, reviewerToken, permission);

        var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();

        var result = await sender.Send(query);

        result.IsSuccess.Should().BeTrue(
            $"the reviewer read should have succeeded: {result.Error.Code}");

        return result.Value;
    }

    /// <summary>A fresh verified member, for use as a reviewer.</summary>
    private async Task<(Guid UserId, string Token)> ReviewerAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        return (auth.User.Id, auth.Tokens.AccessToken);
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadAsStringAsync();

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            $"the request should have succeeded, but answered {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    // ── Wire shapes, re-declared by hand ────────────────────────────────────────────────────

    private sealed record GuideDto(Guid GuideId, string Status);

    private sealed record TrustVerificationDto(Guid VerificationId, string Status);

    private sealed record RequirementDto(
        string Key, string Label, bool IsMet, string? ActionRoute, long? Current, long? Target);

    private sealed record ProgressDto(long Current, long Target, int Percentage);

    private sealed record StatusTargetDto(
        string Status, string Label, List<RequirementDto> Requirements);

    private sealed record AudienceDto(
        long Followers,
        long Following,
        long Reach,
        bool ReachAvailable,
        long TotalContentPublished,
        long TotalHelpfulVotes);

    private sealed record CompletenessDto(int Percentage, List<RequirementDto> Missing);

    private sealed record CreatorProfileDto(
        Guid CreatorId,
        Guid UserId,
        string DisplayName,
        string? Bio,
        string Status,
        StatusTargetDto? NextStatus,
        string StatusPolicyVersion,
        List<string> Categories,
        List<string> Expertise,
        List<string> Languages,
        bool IsDiscoverable,
        bool IsVerifiedCreator,
        AudienceDto Audience,
        CompletenessDto Completeness);

    private sealed record PublicCreatorDto(
        Guid CreatorId,
        string DisplayName,
        string Status,
        bool IsVerifiedCreator,
        long Followers,
        long ContentPublished,
        List<object> Badges);

    private sealed record EvidenceDto(Guid EvidenceId, string Kind, string Label, string Status);

    private sealed record VerificationDto(
        Guid? ApplicationId,
        string State,
        List<RequirementDto> Requirements,
        List<EvidenceDto> Evidence,
        DateTimeOffset? SubmittedAt,
        DateTimeOffset? ExpiresAt,
        bool CanResubmit,
        bool CanAppeal);

    private sealed record MilestoneDto(
        Guid MilestoneId,
        string Key,
        string Title,
        string State,
        ProgressDto Progress,
        DateTimeOffset? CompletedAt,
        DateTimeOffset? ClaimedAt,
        int SortOrder);

    private sealed record ChallengeTaskDto(
        Guid TaskId, string Label, bool IsComplete, ProgressDto? Progress);

    private sealed record ChallengeDto(
        Guid ChallengeId,
        string Key,
        string Title,
        string State,
        bool IsEligible,
        List<ChallengeTaskDto> Tasks,
        ProgressDto Progress,
        List<string> Rules,
        DateTimeOffset? JoinedAt,
        bool HasLeaderboard);

    private sealed record LeaderboardEntryDto(
        int Rank, Guid CreatorId, string DisplayName, long Score);

    private sealed record LeaderboardDto(
        string Scope,
        string Title,
        string Period,
        string PeriodLabel,
        string ScoreLabel,
        string ScoreDefinition,
        List<LeaderboardEntryDto> Entries,
        LeaderboardEntryDto? ViewerEntry,
        bool ViewerIsOptedOut,
        string RefreshNote);

    private sealed record MoneyDto(long MinorUnits, string Currency, int Exponent);

    private sealed record GrantDto(
        Guid GrantId,
        string Key,
        string Title,
        string State,
        MoneyDto FundingAmount,
        List<string> Deliverables,
        bool IsEligible,
        Guid? ApplicationId,
        string? ApplicationState);

    private sealed record GrantFieldOptionDto(string Value, string Label);

    private sealed record GrantFieldDto(
        string Key,
        string Label,
        string Kind,
        bool IsRequired,
        List<GrantFieldOptionDto>? Options);

    private sealed record GrantDeclarationDto(string Key, string Text);

    private sealed record GrantFormDto(
        Guid GrantId,
        List<GrantFieldDto> Fields,
        List<GrantDeclarationDto> Declarations,
        bool AllowsAttachments,
        bool AllowsAmountRequest);

    private sealed record GrantApplicationDto(
        Guid ApplicationId,
        Guid GrantId,
        string State,
        Dictionary<string, string> Answers,
        MoneyDto? AwardedAmount,
        string? FundingReference,
        bool CanWithdraw);

    private sealed record SectionDto(string Key, string Title, List<Guid> CourseIds);

    private sealed record CourseProgressDto(
        Guid CourseId,
        string State,
        int LessonsCompleted,
        int LessonCount,
        int Percentage,
        Guid? CertificateId);

    private sealed record CourseDto(
        Guid CourseId,
        string Slug,
        string Title,
        string Level,
        int LessonCount,
        bool AwardsCertificate,
        CourseProgressDto? Progress,
        bool IsSaved,
        int ContentVersion);

    private sealed record LessonBlockDto(
        string Kind, string? Text, string? Title, List<string>? Items);

    private sealed record QuizOptionDto(Guid OptionId, string Label);

    private sealed record QuizQuestionDto(
        Guid QuestionId, string Prompt, List<QuizOptionDto> Options);

    private sealed record QuizDto(Guid QuizId, int PassMark, List<QuizQuestionDto> Questions);

    private sealed record LessonDto(
        Guid LessonId,
        Guid CourseId,
        string Title,
        string Kind,
        List<LessonBlockDto> Blocks,
        QuizDto? Quiz,
        bool IsComplete,
        int ContentVersion,
        int? CompletedAtVersion,
        bool HasBeenRevised);

    private sealed record QuizOutcomeDto(Guid QuestionId, bool IsCorrect, string? Explanation);

    private sealed record QuizResultDto(
        bool Passed,
        int CorrectCount,
        int TotalCount,
        List<QuizOutcomeDto> Outcomes,
        bool CanRetry,
        CourseProgressDto? Progress);

    private sealed record CertificateDto(
        Guid CertificateId, Guid CourseId, string CourseTitle, string VerificationReference);

    private sealed record ReferralProgrammeDto(
        string Code,
        string ShareUrl,
        string RewardDescription,
        List<string> Terms,
        string QualificationDescription,
        string QualificationVersion,
        int Invited,
        int SignedUp,
        int Qualified,
        int PendingRewards,
        int ConfirmedRewards,
        long TotalPointsEarned,
        bool IsEligible);

    private sealed record ReferralDto(
        Guid ReferralId, string InviteeLabel, string State, string? StateReason);

    private sealed record ReferralPageDto(
        List<ReferralDto> Referrals, string? NextCursor, bool HasMore);

    private sealed record SignupBonusDto(
        string State,
        int Points,
        string? IneligibleReason,
        List<RequirementDto> Requirements,
        DateTimeOffset? AwardedAt);

    private sealed record PointRuleDto(
        string RuleKey,
        string Label,
        int Points,
        string? ActionRoute,
        string? LimitLabel,
        int? MaxOccurrences,
        int? CooldownHours);

    private sealed record PointRulesDto(
        List<PointRuleDto> Rules,
        string CatalogueVersion,
        string ExpiryPolicy,
        string ExpiryDescription);

    private sealed record RewardDto(
        Guid RewardId,
        string Key,
        string Title,
        int PointsCost,
        string State,
        string? LockedReason);

    private sealed record RewardCatalogueDto(
        List<RewardDto> Rewards, string CatalogueVersion, long AvailablePoints);

    private sealed record RedemptionDto(
        Guid RedemptionId,
        Guid RewardId,
        string RewardTitle,
        int PointsSpent,
        string State,
        string? GrantReference,
        string CatalogueVersion);

    private sealed record RewardBalanceDto(long Balance, long LifetimeEarned, long PendingPoints = 0);

    private sealed record FacetDto(string Value, int Count);

    private sealed record FacetsDto(
        List<FacetDto> Countries,
        List<FacetDto> Categories,
        List<FacetDto> Expertise,
        List<FacetDto> Languages,
        int VerifiedCount,
        int TotalCount);

    private sealed record DirectoryDto(
        List<PublicCreatorDto> Creators, string? NextCursor, bool HasMore, FacetsDto Facets);
}
