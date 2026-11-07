using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Contracts.Identity.V1.Requests;
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
/// The Trust &amp; Verification Engine, driven over real HTTP through the same host every
/// other suite uses.
/// <para>
/// Two things are under test at once. The obvious one is the module's behaviour. The less
/// obvious — and the reason this suite matters to B2.6 as much as to B3 — is that a module
/// of this size, with fourteen tables, ten permissions and twenty-six routes, composed into
/// the platform through nothing but contributor interfaces and one project reference. The
/// Bookmarks suite proved a toy could. This proves a real one can.
/// </para>
/// </summary>
public sealed class TrustModuleTests : IdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public TrustModuleTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Composition ──────────────────────────────────────────────────────

    /// <summary>
    /// Proof the module's <c>IFeatureContributor</c> reached the composed catalogue: a
    /// Trust-owned gate turns up in Identity's <c>/access/features</c> response, which the
    /// Trust module never touched.
    /// </summary>
    [Fact]
    public async Task The_trust_feature_gates_composed_into_the_platform_catalogue()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        var names = features.Features.Select(feature => feature.Name).ToList();

        names.Should().Contain("trust.center");
        names.Should().Contain("trust.verification.diaspora");
        names.Should().Contain("trust.appeals");
    }

    [Fact]
    public async Task The_platform_manifest_lists_the_trust_module()
    {
        var response = await HttpClient.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("Trust", "the kernel discovered the module by assembly scan");
    }

    // ── Access control ───────────────────────────────────────────────────

    [Fact]
    public async Task Trust_endpoints_require_authentication()
    {
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync("/api/v1/trust/profile");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The review permission is granted to no ordinary member in any access state. A verified
    /// member reaching the staff queue would make every fairness rule in the module
    /// decorative.
    /// </summary>
    [Fact]
    public async Task An_ordinary_member_cannot_reach_the_review_queue()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/trust/verifications/queue");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_ordinary_member_cannot_reach_the_moderation_queue()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/trust/moderation/queue");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// ADR-002's inbound channel is Identity's, not Trust's. This asserts the route is still
    /// served by the controller that owns it — a Trust endpoint shadowing it would break the
    /// one contract the separation depends on.
    /// </summary>
    [Fact]
    public async Task The_trust_decisions_route_is_still_owned_by_identity()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/trust/decisions",
            new { userId = Guid.NewGuid(), outcome = "Granted" },
            JsonOptions);

        // Forbidden, not NotFound: the route exists and refuses a member token, exactly as
        // ADR-002 requires. A 404 would mean Trust had shadowed or displaced it.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Profile ──────────────────────────────────────────────────────────

    /// <summary>
    /// The trust centre must open for a brand new member — it is the screen that tells them
    /// how to become verified.
    /// </summary>
    [Fact]
    public async Task A_new_member_can_open_their_trust_profile()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/trust/profile");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await ReadDataAsync<TrustProfileDto>(response);
        profile.TrustLevel.Should().NotBeNullOrWhiteSpace();
        profile.HealthStatus.Health.Should().NotBeNullOrWhiteSpace();
        profile.Verifications.Should().NotBeEmpty(
            "every supported type appears so the checklist renders a row for each");
    }

    /// <summary>
    /// The risk score is staff-facing. A member receiving it can read it, whatever the client
    /// chooses to render.
    /// </summary>
    [Fact]
    public async Task A_members_own_profile_omits_the_internal_risk_score()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var profile = await ReadDataAsync<TrustProfileDto>(
            await HttpClient.GetAsync("/api/v1/trust/profile"));

        profile.RiskScore.Should().BeNull();
        profile.RiskLevel.Should().BeNull();
    }

    [Fact]
    public async Task A_new_member_can_read_their_reputation()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/trust/reputation/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var reputation = await ReadDataAsync<ReputationDto>(response);
        reputation.Level.Should().Be("Member");
        reputation.CombinedScore.Should().Be(0);
        reputation.NextLevel!.Name.Should().Be("VerifiedResident");
    }

    [Fact]
    public async Task The_feature_requirements_checklist_is_served_for_every_capability()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/trust/feature-requirements?targetCountry=GB");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var requirements = await ReadDataAsync<List<FeatureRequirementDto>>(response);

        requirements.Select(item => item.FeatureId)
            .Should().Contain(["guides", "jobs", "housing", "marketplace"]);

        // Every capability with Trust thresholds serves its checklist. Posting has none — a
        // verified account is enough, and the service that takes the post checks that: jobs since
        // 14 Sep 2026, guides, homes and items since 15 Sep. So they are served empty and unlocked
        // rather than as checklists nobody could complete.
        string[] posting = ["jobs", "guides", "housing", "marketplace"];

        requirements.Where(item => !posting.Contains(item.FeatureId))
            .Should().OnlyContain(item => item.Requirements.Count > 0);

        foreach (var featureId in posting)
        {
            var feature = requirements.Single(item => item.FeatureId == featureId);
            feature.Requirements.Should().BeEmpty(featureId);
            feature.IsUnlocked.Should().BeTrue(featureId);
        }
    }

    // ── Verification ─────────────────────────────────────────────────────

    [Fact]
    public async Task A_member_can_submit_a_diaspora_verification()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/trust/verifications/diaspora",
            new
            {
                nationality = "NG",
                residenceCountry = "GB",
                documentType = "Passport",
                issueCountry = "NG",
                issueDate = "2020-01-01",
                expiryDate = "2030-01-01",
                documentNumber = "AA0012345",
                documentReference = "storage/doc-1",
                selfieReference = "storage/selfie-1"
            },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var verification = await ReadDataAsync<VerificationDto>(response);
        verification.Type.Should().Be("Diaspora");
        verification.Status.Should().Be("Pending", "it enters the review queue");
        verification.Nationality.Should().Be("NG");
        verification.ResidenceCountry.Should().Be("GB");
    }

    /// <summary>
    /// The platform's defining rule, enforced end to end: Diaspora is for people living
    /// outside their country of origin. The frontend refuses this client-side too, but the
    /// server is what decides.
    /// </summary>
    [Fact]
    public async Task A_member_living_in_their_country_of_origin_is_refused_automatically()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/trust/verifications/diaspora",
            new
            {
                nationality = "GB",
                residenceCountry = "GB",
                documentType = "Passport",
                issueCountry = "GB",
                documentReference = "storage/doc-1",
                selfieReference = "storage/selfie-1"
            },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var verification = await ReadDataAsync<VerificationDto>(response);
        verification.Status.Should().Be("Rejected");
        verification.RejectionReasonCode.Should().Be("NOT_DIASPORA_ELIGIBLE");
        verification.CanAppeal.Should().BeTrue("a member may always challenge a refusal");
    }

    [Fact]
    public async Task A_second_open_verification_of_the_same_type_is_refused()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await SubmitCountryVerification();
        var second = await SubmitCountryVerificationRaw();

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await second.Content.ReadAsStringAsync();
        body.Should().Contain("trust.verification.duplicate");
    }

    [Fact]
    public async Task A_member_can_list_and_read_their_own_verifications()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var submitted = await SubmitCountryVerification();

        var listed = await ReadDataAsync<PagedDto<VerificationDto>>(
            await HttpClient.GetAsync("/api/v1/trust/verifications"));

        listed.Items.Should().ContainSingle().Which.VerificationId.Should().Be(submitted.VerificationId);

        var detail = await HttpClient.GetAsync(
            $"/api/v1/trust/verifications/{submitted.VerificationId}");

        detail.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Confirming that a verification exists for someone else is itself a disclosure, so the
    /// module answers not-found rather than forbidden.
    /// </summary>
    [Fact]
    public async Task A_member_cannot_read_someone_elses_verification()
    {
        var (_, ownerAuth) = await RegisterAndVerifyAsync();
        Api.Authenticate(ownerAuth.Tokens.AccessToken);
        var submitted = await SubmitCountryVerification();

        var (_, otherAuth) = await RegisterAndVerifyAsync();
        Api.Authenticate(otherAuth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            $"/api/v1/trust/verifications/{submitted.VerificationId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Submitting_a_verification_writes_the_trust_timeline()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await SubmitCountryVerification();

        var timeline = await ReadDataAsync<PagedDto<TimelineEventDto>>(
            await HttpClient.GetAsync("/api/v1/trust/timeline"));

        timeline.Items.Should().NotBeEmpty();
        timeline.Items.Should().Contain(item => item.Type == "VerificationSubmitted");
        timeline.Items.Should().OnlyContain(item => !string.IsNullOrWhiteSpace(item.Icon));
    }

    // ── Appeals ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_member_can_appeal_a_rejected_verification()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        // An origin-country submission is auto-rejected, giving us something appealable.
        var rejected = await ReadDataAsync<VerificationDto>(
            await HttpClient.PostAsJsonAsync(
                "/api/v1/trust/verifications/diaspora",
                new
                {
                    nationality = "GB",
                    residenceCountry = "GB",
                    documentType = "Passport",
                    issueCountry = "GB",
                    documentReference = "storage/doc-1",
                    selfieReference = "storage/selfie-1"
                },
                JsonOptions));

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/trust/appeals",
            new
            {
                type = "Verification",
                targetId = rejected.VerificationId.ToString(),
                reason = "My nationality was recorded incorrectly during onboarding."
            },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var appeal = await ReadDataAsync<AppealDto>(response);
        appeal.Status.Should().Be("Pending");
        appeal.Explanation.Should().NotBeNullOrWhiteSpace(
            "the appeals screen renders this as reviewer comments and must never show a blank");
    }

    [Fact]
    public async Task A_second_appeal_against_the_same_target_is_refused()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        // A publishing restriction, whose target Trust does not check. Until 7 Nov 2026 this used a
        // suspension with the made-up target "suspension-1"; a suspension appeal now names the
        // appellant's own account and is refused unless they are suspended (AccountAdministrationTests).
        var body = new
        {
            type = "Publishing",
            targetId = "publishing-1",
            reason = "I was restricted in error and would like this reviewed."
        };

        var first = await HttpClient.PostAsJsonAsync("/api/v1/trust/appeals", body, JsonOptions);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await HttpClient.PostAsJsonAsync("/api/v1/trust/appeals", body, JsonOptions);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await second.Content.ReadAsStringAsync()).Should().Contain("trust.appeal.already-open");
    }

    [Fact]
    public async Task An_appeal_shorter_than_the_minimum_is_rejected_with_a_validation_problem()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/trust/appeals",
            new { type = "Content", targetId = "guide-1", reason = "unfair" },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// NG-42. The endpoint used to compare <c>decision</c> to the literal "Approve" and let
    /// everything else fall through to <c>Upheld: false</c>, so a reviewer who typed
    /// "approved" declined the appeal and was told it had succeeded. Nobody on either side
    /// could see that the decision had not been understood. A decision the server cannot
    /// name must now decide nothing and say so.
    /// <para>
    /// The appeal id is arbitrary because the refusal happens while translating the request,
    /// before the command reaches the handler that would look the appeal up — which is the
    /// distinction this test is pinning: a 400 rather than the 404 a real lookup would give.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("approved")]
    [InlineData("Approved")]
    [InlineData("Uphold")]
    [InlineData("")]
    public async Task An_appeal_review_decision_the_server_cannot_name_is_refused(string decision)
    {
        await AuthenticateAsModeratorAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"/api/v1/trust/appeals/{Guid.NewGuid()}/review",
            new { decision, explanation = "Reviewed against the original evidence." },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await response.Content.ReadAsStringAsync())
            .Should().Contain("trust.appeal.decision-unrecognised");
    }

    /// <summary>
    /// The other half of NG-42: the three decisions the contract documents must still be
    /// understood, in any casing, so that tightening the parse did not quietly narrow the
    /// endpoint. None of these reaches a real appeal, so a 404 from the handler is the
    /// success signal — it means the decision parsed and the command was dispatched.
    /// </summary>
    [Theory]
    [InlineData("Approve")]
    [InlineData("approve")]
    [InlineData("Reject")]
    [InlineData("Escalate")]
    public async Task Every_documented_appeal_review_decision_still_parses(string decision)
    {
        await AuthenticateAsModeratorAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"/api/v1/trust/appeals/{Guid.NewGuid()}/review",
            new { decision, explanation = "Reviewed against the original evidence." },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await response.Content.ReadAsStringAsync())
            .Should().Contain("trust.appeal.not-found");
    }

    [Fact]
    public async Task A_member_can_list_their_appeals()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        await HttpClient.PostAsJsonAsync(
            "/api/v1/trust/appeals",
            new
            {
                type = "Publishing",
                targetId = "publishing-1",
                reason = "I believe I now meet the publishing requirements."
            },
            JsonOptions);

        var listed = await ReadDataAsync<PagedDto<AppealDto>>(
            await HttpClient.GetAsync("/api/v1/trust/appeals"));

        listed.Items.Should().ContainSingle();
    }

    // ── Eligibility ──────────────────────────────────────────────────────

    /// <summary>
    /// The vocabulary the client's screens already branch on, now decided server-side.
    /// </summary>
    [Fact]
    public async Task Eligibility_returns_the_code_vocabulary_the_client_expects()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/trust/eligibility",
            new { capability = "guides", targetCountry = "GB", dryRun = true },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var decision = await ReadDataAsync<EligibilityDto>(response);

        decision.Code.Should().BeOneOf(
            "ALLOW", "DENIED", "RATE_LIMITED", "COOLDOWN", "HEALTH_RESTRICTED");
    }

    /// <summary>
    /// A member with no verification cannot join the guide programme, and is told which
    /// requirements they have not met rather than simply refused.
    /// <para>
    /// This used guide publishing until 15 Sep 2026, when publishing a guide stopped carrying Trust
    /// thresholds. The guide programme is the capability that still asks for them.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_unverified_member_is_refused_the_guide_programme_with_named_requirements()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var decision = await ReadDataAsync<EligibilityDto>(
            await HttpClient.PostAsJsonAsync(
                "/api/v1/trust/eligibility",
                new { capability = "guide-programme", targetCountry = "GB", dryRun = true },
                JsonOptions));

        decision.Allowed.Should().BeFalse();
        decision.Code.Should().Be("DENIED");
        decision.UnmetRequirements.Should().NotBeEmpty(
            "the client renders these as an unlock checklist");
    }

    /// <summary>
    /// Trust does not know every capability on the platform. A module that has not declared a
    /// requirement has not asked to be gated, so refusing by default would break every
    /// ungated action the moment this endpoint shipped.
    /// </summary>
    [Fact]
    public async Task An_undeclared_capability_is_allowed_rather_than_refused()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var decision = await ReadDataAsync<EligibilityDto>(
            await HttpClient.PostAsJsonAsync(
                "/api/v1/trust/eligibility",
                new { capability = "some-future-module-capability", dryRun = true },
                JsonOptions));

        decision.Allowed.Should().BeTrue();
        decision.Code.Should().Be("ALLOW");
    }

    // ── Envelope ─────────────────────────────────────────────────────────

    /// <summary>
    /// Every Trust response goes through the same <c>PlatformResults</c> helper Identity's
    /// controllers use, so a client parses one shape across the whole platform.
    /// </summary>
    [Fact]
    public async Task Trust_responses_use_the_platform_success_envelope()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/trust/profile");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("\"data\"");
        body.Should().Contain("\"meta\"");
        body.Should().Contain("\"correlationId\"");
    }

    [Fact]
    public async Task Trust_errors_use_the_platform_problem_document()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            $"/api/v1/trust/verifications/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"code\"");
        body.Should().Contain("trust.verification.not-found");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task<VerificationDto> SubmitCountryVerification() =>
        await ReadDataAsync<VerificationDto>(await SubmitCountryVerificationRaw());

    private Task<HttpResponseMessage> SubmitCountryVerificationRaw() =>
        HttpClient.PostAsJsonAsync(
            "/api/v1/trust/verifications/country",
            new
            {
                countryCode = "GB",
                documentType = "ResidencePermit",
                documentReference = "storage/permit-1",
                arrivalDate = "2022-03-15",
                expertiseAreas = new[] { "visas", "banking" }
            },
            JsonOptions);

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

    private sealed record Envelope<T>(T Data);

    private sealed record PagedDto<T>(List<T> Items, int Page, int PageSize, int TotalCount);

    private sealed record TrustProfileDto(
        Guid UserId,
        string TrustLevel,
        HealthDto HealthStatus,
        int? RiskScore,
        string? RiskLevel,
        Dictionary<string, string> Verifications,
        List<BadgeDto> Badges);

    /// <summary>
    /// Authenticates as somebody who may actually review an appeal.
    /// <para>
    /// <c>trust:appeal:review</c> is granted to the Moderator, Admin and SuperAdmin roles at
    /// <c>TrustContributors.cs</c>, above a Verified access tier. The role is assigned through
    /// the aggregate's own <c>AssignRole</c> rather than by writing a column, and the re-login
    /// is what puts the claim into the JWT the policy reads — the same approach the Guides
    /// suite takes, and the reason it is duplicated here is that <see cref="IdentityTestBase"/>
    /// still offers no staff helper.
    /// </para>
    /// </summary>
    private async Task AuthenticateAsModeratorAsync()
    {
        var (phone, auth) = await RegisterVerifiedMemberAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

            var userId = new UserId(auth.User.Id);

            var user = await dbContext.Set<User>()
                .FirstOrDefaultAsync(candidate => candidate.Id == userId);

            user.Should().NotBeNull("the member was registered a moment ago");

            user!.AssignRole(UserRole.All.Single(role => role.Name == "Moderator"));

            await dbContext.SaveChangesAsync();
        }

        var login = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(
            await Api.LoginAsync(new LoginRequest { Email = EmailFor(phone), Password = ValidPassword }));

        Api.Authenticate(login.Tokens.AccessToken);
    }

    private sealed record HealthDto(
        string Health, int ViolationsCount, int WarningsCount, int ActiveAppealsCount);

    private sealed record BadgeDto(string Type, string Label, int? Since, string? CountryCode);

    private sealed record VerificationDto(
        Guid VerificationId,
        Guid UserId,
        string Type,
        string Status,
        string? Nationality,
        string? ResidenceCountry,
        string? TargetCountry,
        string? RejectionReasonCode,
        bool CanAppeal);

    private sealed record ReputationDto(
        string Level, int CombinedScore, NextLevelDto? NextLevel);

    private sealed record NextLevelDto(string Name, int MinScore, int PointsNeeded);

    private sealed record AppealDto(
        Guid AppealId, string Type, string TargetId, string Status, string? Explanation);

    private sealed record TimelineEventDto(
        Guid EventId, string Type, string Label, string Icon);

    private sealed record EligibilityDto(
        bool Allowed, string Code, string? Reason, int? WaitTimeSec, List<string> UnmetRequirements);

    private sealed record FeatureRequirementDto(
        string FeatureId, string Name, bool IsUnlocked, List<RequirementItemDto> Requirements);

    private sealed record RequirementItemDto(string Description, bool IsMet, string CurrentProgress);
}
