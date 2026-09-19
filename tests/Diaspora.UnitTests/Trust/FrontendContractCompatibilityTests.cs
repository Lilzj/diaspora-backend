using System.Reflection;
using System.Text.Json;
using Diaspora.Modules.Trust.Contracts.V1.Requests;
using Diaspora.Modules.Trust.Contracts.V1.Responses;
using Diaspora.Modules.Trust.Domain;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Trust;

/// <summary>
/// The API contract freeze, made executable.
/// <para>
/// Every literal below is copied from the Diaspora frontend, with its source file cited. The
/// frontend is the authoritative source for user-facing contracts, and these tests are what
/// stop the backend drifting away from it silently — a renamed enum member or a
/// re-cased property is caught here rather than by a member seeing a blank badge.
/// </para>
/// <para>
/// <b>When one of these fails</b>, the fix is almost never to change the test. It is either
/// to restore the backend name, or — if the change is deliberate — to update the frontend,
/// this test, and the compatibility matrix in
/// <c>docs/trust/frontend-contract-report.md</c> together, in one reviewed change. That is
/// what "frozen" means here: not unchangeable, but not changeable by accident.
/// </para>
/// </summary>
public sealed class FrontendContractCompatibilityTests
{
    /// <summary>
    /// The API serialises camelCase (configured in <c>Program.cs</c>), so a property named
    /// <c>VerificationId</c> reaches the client as <c>verificationId</c>. These tests compare
    /// against the frontend's spelling after that transform.
    /// </summary>
    private static readonly JsonSerializerOptions ApiJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static IEnumerable<string> WireNamesOf<T>() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => ApiJson.PropertyNamingPolicy!.ConvertName(property.Name));

    private static string[] NamesOf<TEnum>() where TEnum : struct, Enum =>
        Enum.GetNames<TEnum>();

    // ── Enums ────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>src/types/trust.ts:5</c>. The client uses these as object keys in
    /// <c>Record&lt;VerificationType, VerificationStatus&gt;</c>, so a rename does not fail
    /// loudly — it renders an empty row.
    /// </summary>
    [Fact]
    public void VerificationType_covers_every_value_the_frontend_declares()
    {
        string[] frontend =
        [
            "Phone", "Email", "Identity", "Country",
            "Professional", "Business", "Organization", "Embassy"
        ];

        NamesOf<VerificationType>().Should().Contain(frontend);
    }

    /// <summary>
    /// <c>src/types/trust.ts:15</c> and <c>src/types/verification.ts:12</c> — the frontend
    /// declares this enum twice with different members. The backend has one, and it must
    /// carry the union.
    /// </summary>
    [Fact]
    public void VerificationStatus_covers_both_frontend_declarations()
    {
        string[] frontend = ["Pending", "Approved", "Rejected", "NeedMoreInfo", "Expired"];

        NamesOf<VerificationStatus>().Should().Contain(frontend);
    }

    /// <summary>
    /// <c>src/types/trust.ts:31</c>. The diaspora wizard's status vocabulary. Every value the
    /// client can render must be reachable on the server.
    /// </summary>
    [Fact]
    public void VerificationStatus_covers_the_diaspora_wizard_statuses()
    {
        // 'NotStarted' is client-only — it means "no request exists", which the server
        // expresses by there being no row. 'RequiresAdditionalDocuments' is the client's
        // spelling of NeedMoreInfo; both map through the compatibility matrix.
        string[] frontend = ["Pending", "UnderReview", "Approved", "Rejected", "Expired", "Appealed"];

        NamesOf<VerificationStatus>().Should().Contain(frontend);
    }

    /// <summary>
    /// <c>src/types/verification.ts:62</c> and <c>:5</c> — the two document-type unions,
    /// which overlap but disagree. Both are carried so neither screen has to change.
    /// </summary>
    [Fact]
    public void VerificationDocumentType_covers_both_frontend_unions()
    {
        string[] diasporaWizard =
        [
            "Passport", "ResidencePermit", "WorkPermit", "OccupationPermit",
            "StudentPermit", "Visa", "GovernmentImmigrationDocuments", "Other"
        ];

        string[] countryScreen =
        [
            "PassportStamp", "ResidencePermit", "WorkPermit", "StudentVisa", "Other"
        ];

        var backend = NamesOf<VerificationDocumentType>().ToList();

        backend.Should().Contain(diasporaWizard);
        backend.Should().Contain(countryScreen);
    }

    /// <summary>
    /// <c>src/app/verification/identity.tsx:123</c> — the identity screen offers six document
    /// types that appear in no shared type at all.
    /// </summary>
    [Fact]
    public void VerificationDocumentType_covers_the_identity_screens_options()
    {
        string[] identityScreen =
        [
            "Passport", "DriverLicense", "NationalId",
            "WorkPermit", "ResidencePermit", "StudentPermit"
        ];

        NamesOf<VerificationDocumentType>().Should().Contain(identityScreen);
    }

    /// <summary>
    /// <c>src/types/verification.ts:90</c>. SCREAMING_SNAKE, compared as string literals by
    /// the client.
    /// </summary>
    [Fact]
    public void RejectionReason_codes_keep_the_frontends_screaming_snake_casing()
    {
        string[] frontend =
        [
            "INVALID_DOCUMENT", "EXPIRED_DOCUMENT",
            "MISMATCHED_NATIONALITY", "ADDITIONAL_DOCS_NEEDED"
        ];

        NamesOf<VerificationRejectionReason>().Should().Contain(frontend);
    }

    /// <summary>
    /// <c>src/types/trust.ts:41</c>. The frontend spells the last one <c>'Under Review'</c>,
    /// with a space — the one documented deviation in this enum, because a space cannot
    /// survive enum binding.
    /// </summary>
    [Fact]
    public void AccountHealth_matches_the_frontend_except_for_the_documented_space()
    {
        var backend = NamesOf<AccountHealth>().ToList();

        backend.Should().Contain(["Excellent", "Good", "Restricted", "Suspended"]);
        backend.Should().Contain("UnderReview");
        backend.Should().NotContain("Under Review", "an enum member cannot contain a space");
    }

    /// <summary>
    /// <c>src/types/trust.ts:73</c> and <c>:75</c>. The risk factors render directly as chips,
    /// so their exact casing is load-bearing.
    /// </summary>
    [Fact]
    public void Risk_vocabulary_matches_the_frontend_exactly()
    {
        NamesOf<RiskLevel>().Should().BeEquivalentTo("Low", "Medium", "High");

        NamesOf<RiskFactor>().Should().BeEquivalentTo(
            "VPN_Detected", "Device_Change", "Rapid_Posting",
            "Duplicate_Content", "Failed_Verification", "Reports_Spike");
    }

    /// <summary>
    /// <c>src/types/reputation.ts:5</c>. The frontend contradicts itself — its
    /// <c>CountryReputationScore.level</c> (<c>trust.ts:59</c>) ends in <c>Ambassador</c>
    /// for the same ladder. The backend emits the longer name, which is the one with
    /// thresholds, labels and icons defined against it.
    /// </summary>
    [Fact]
    public void ReputationLevel_matches_the_authoritative_frontend_declaration()
    {
        NamesOf<ReputationLevel>().Should().BeEquivalentTo(
            "Member", "VerifiedResident", "TrustedContributor",
            "CommunityExpert", "CommunityAmbassador");

        NamesOf<ReputationLevel>().Should().NotContain("Ambassador",
            "the CountryReputationScore spelling is the frontend's own inconsistency");
    }

    /// <summary>
    /// <c>src/types/reputation.ts:12</c>. Lowercase, because the client keys its
    /// <c>DIMENSION_LABELS</c> map on these.
    /// </summary>
    [Fact]
    public void ReputationDimension_stays_lowercase()
    {
        NamesOf<ReputationDimension>().Should().BeEquivalentTo(
            "contribution", "verification", "trust");
    }

    /// <summary><c>src/types/reputation.ts:68</c>.</summary>
    [Fact]
    public void BadgeType_covers_every_badge_the_frontend_declares()
    {
        string[] frontend =
        [
            "CountryVerified", "PhoneVerified", "EmailVerified", "GovernmentIdVerified",
            "ProfilePhoto", "ProviderVerified", "TopContributor"
        ];

        NamesOf<BadgeType>().Should().Contain(frontend);
    }

    /// <summary>The badges the brief names, beyond what the client renders today.</summary>
    [Fact]
    public void BadgeType_covers_every_badge_the_brief_names()
    {
        string[] brief =
        [
            "VerifiedMember", "TrustedContributor", "CommunityGuide",
            "VerifiedBusiness", "VerifiedOrganization", "EarlyMember", "Moderator"
        ];

        NamesOf<BadgeType>().Should().Contain(brief);
    }

    /// <summary><c>src/types/trust.ts:153</c> and <c>:156</c>.</summary>
    [Fact]
    public void Appeal_vocabulary_matches_the_frontend()
    {
        NamesOf<AppealType>().Should().BeEquivalentTo(
            "Verification", "Content", "Suspension", "Publishing");

        // The client's own union is only these three; the rest are the states its six-stage
        // workflow tracker already draws but had no data for.
        NamesOf<AppealStatus>().Should().Contain(["Pending", "Approved", "Rejected"]);
    }

    /// <summary>
    /// <c>src/types/trust.ts:89</c> — the six stages the appeals screen renders as a tracker.
    /// </summary>
    [Fact]
    public void ModerationQueueStatus_covers_the_workflow_tracker_stages()
    {
        string[] frontend =
        [
            "PendingReview", "Approved", "Rejected", "Escalated", "Appealed", "Resolved"
        ];

        NamesOf<ModerationQueueStatus>().Should().Contain(frontend);
    }

    /// <summary><c>src/types/moderation.ts:5</c> and <c>:7</c>.</summary>
    [Fact]
    public void Moderation_vocabulary_matches_the_frontend()
    {
        NamesOf<ModerationContentType>().Should().Contain(
            ["Guide", "CommunityUpdate", "Comment"]);

        NamesOf<ModerationActionType>().Should().Contain(
            ["Approve", "Reject", "Hide", "Archive", "Pin", "Unpin", "Suspend", "Restore"]);
    }

    /// <summary>
    /// <c>src/lib/trustEngine.ts:110</c>. The exact literals the client's screens branch on.
    /// Moving the decision server-side only works if this vocabulary survives the move — this
    /// is the single most important compatibility assertion in the file.
    /// </summary>
    [Fact]
    public void EligibilityCode_matches_the_client_trust_engine_exactly()
    {
        NamesOf<EligibilityCode>().Should().BeEquivalentTo(
            "ALLOW", "DENIED", "RATE_LIMITED", "COOLDOWN", "HEALTH_RESTRICTED");
    }

    /// <summary>
    /// Fixed by Identity's published <c>ApplyTrustDecisionRequest</c>. A value here that Identity
    /// cannot parse is refused at runtime, which is what this guards.
    /// <para>
    /// <c>Submitted</c> joined both ends together. The access ladder is
    /// <c>LimitedAccess -> VerificationPending -> VerificationUnderReview -> Verified</c>, and
    /// submission used to publish <c>UnderReview</c> — the third rung, unreachable from the
    /// first — so no submission ever moved anybody. This enum is internal to the Trust-to-Identity
    /// hop; no client parses it, which is why a fifth value is safe to add and a sixth would need
    /// the same check.
    /// </para>
    /// </summary>
    [Fact]
    public void TrustDecisionOutcome_matches_the_identity_contract_exactly()
    {
        NamesOf<TrustDecisionOutcome>().Should().BeEquivalentTo(
            "Submitted", "UnderReview", "Granted", "Rejected", "Revoked");
    }

    // ── Response DTOs ────────────────────────────────────────────────────

    /// <summary><c>src/types/verification.ts:84</c>.</summary>
    [Fact]
    public void VerificationResponse_carries_every_field_the_frontend_reads()
    {
        string[] frontend =
        [
            "verificationId", "userId", "status", "submittedAt",
            "reviewedAt", "rejectionReasonCode", "rejectionReasonDetails"
        ];

        WireNamesOf<VerificationResponse>().Should().Contain(frontend);
    }

    /// <summary><c>src/types/trust.ts:43</c>.</summary>
    [Fact]
    public void AccountHealthStatus_carries_every_field_the_frontend_reads()
    {
        string[] frontend =
        [
            "health", "violationsCount", "warningsCount",
            "activeAppealsCount", "spamReportsCount", "reasons"
        ];

        WireNamesOf<AccountHealthStatusResponse>().Should().BeEquivalentTo(frontend);
    }

    /// <summary>
    /// <c>src/types/reputation.ts:14</c> — all thirty fields. The reputation screen renders
    /// each as its own line, so a missing one is a missing row.
    /// </summary>
    [Fact]
    public void ReputationProfileResponse_carries_all_thirty_UserReputation_fields()
    {
        string[] frontend =
        [
            "userId",
            "contributionScore", "verificationScore", "trustScore", "combinedScore",
            "level", "nextLevel",
            "guidesPublished", "guidesApproved", "updatesPublished", "updatesVerified",
            "commentsWritten", "guidesUpdated",
            "phoneVerified", "emailVerified", "governmentIdVerified", "profilePhotoAdded",
            "profileCompleted", "providerVerified", "countryVerificationsCount",
            "helpfulVotesReceived", "communityConfirmationsReceived", "violationCount",
            "reportsResolved", "noViolationStreakDays",
            "badges", "levelBenefits", "lastCalculatedAt", "levelChangedAt"
        ];

        WireNamesOf<ReputationProfileResponse>().Should().Contain(frontend);
    }

    /// <summary><c>src/types/reputation.ts:74</c>.</summary>
    [Fact]
    public void LevelBenefits_carries_every_field_the_frontend_reads()
    {
        string[] frontend =
        [
            "canPublishImmediately", "canModerate", "isFeaturedEligible",
            "isRewardsEligible", "hasBetaAccess", "hasExclusiveBadge", "searchBoostFactor"
        ];

        WireNamesOf<LevelBenefitsResponse>().Should().BeEquivalentTo(frontend);
    }

    /// <summary><c>src/types/reputation.ts:67</c>.</summary>
    [Fact]
    public void BadgeResponse_carries_every_field_the_frontend_reads()
    {
        WireNamesOf<BadgeResponse>().Should().Contain(
            ["type", "label", "since", "countryCode"]);
    }

    /// <summary><c>src/types/trust.ts:54</c>.</summary>
    [Fact]
    public void CountryStandingResponse_matches_CountryReputationScore()
    {
        string[] frontend = ["countryCode", "countryName", "flag", "points", "level", "stars"];

        WireNamesOf<CountryStandingResponse>().Should().Contain(frontend);
    }

    /// <summary><c>src/types/trust.ts:63</c>.</summary>
    [Fact]
    public void CountryExperienceResponse_matches_CountryExperienceInterval()
    {
        string[] frontend =
        [
            "countryCode", "countryName", "flag", "startYear", "endYear", "isVerified", "role"
        ];

        WireNamesOf<CountryExperienceResponse>().Should().Contain(frontend);
    }

    /// <summary><c>src/types/trust.ts:97</c>.</summary>
    [Fact]
    public void TrustTimelineEventResponse_matches_TrustTimelineEvent()
    {
        string[] frontend = ["eventId", "type", "label", "timestamp", "icon", "description"];

        WireNamesOf<TrustTimelineEventResponse>().Should().Contain(frontend);
    }

    /// <summary><c>src/types/trust.ts:112</c> and <c>:106</c>.</summary>
    [Fact]
    public void FeatureRequirementResponse_matches_FeatureUnlockRequirement()
    {
        WireNamesOf<FeatureRequirementResponse>().Should().BeEquivalentTo(
            "featureId", "name", "isUnlocked", "requirements");

        WireNamesOf<RequirementItemResponse>().Should().BeEquivalentTo(
            "description", "isMet", "currentProgress");
    }

    /// <summary><c>src/types/trust.ts:151</c>.</summary>
    [Fact]
    public void AppealResponse_carries_every_field_the_appeals_screen_renders()
    {
        string[] frontend =
        [
            "appealId", "type", "targetId", "reason", "status", "explanation", "createdAt"
        ];

        WireNamesOf<AppealResponse>().Should().Contain(frontend);
    }

    /// <summary><c>src/types/moderation.ts:78</c>.</summary>
    [Fact]
    public void ModerationQueueItemResponse_matches_ModerationQueueItem()
    {
        string[] frontend =
        [
            "contentType", "contentId", "title", "authorId", "authorLevel",
            "authorViolationCount", "aiReview", "reportCount", "status", "createdAt"
        ];

        WireNamesOf<ModerationQueueItemResponse>().Should().Contain(frontend);
    }

    /// <summary><c>src/types/moderation.ts:38</c>.</summary>
    [Fact]
    public void AiReviewResponse_matches_AIReview()
    {
        WireNamesOf<AiReviewResponse>().Should().Contain(
            ["status", "flags", "confidence", "processedAt"]);
    }

    /// <summary>
    /// <c>src/lib/trustEngine.ts:108</c> — the shape the client destructures from
    /// <c>evaluateAction</c>.
    /// </summary>
    [Fact]
    public void EligibilityDecisionResponse_matches_the_client_trust_engine_result()
    {
        WireNamesOf<EligibilityDecisionResponse>().Should().BeEquivalentTo(
            "allowed", "code", "reason", "waitTimeSec", "unmetRequirements");
    }

    // ── Request DTOs ─────────────────────────────────────────────────────

    /// <summary>
    /// <c>src/types/verification.ts:72</c>, plus <c>documentNumber</c> — which the wizard
    /// collects (<c>diaspora.tsx:40</c>), validates (<c>:169</c>) and displays (<c>:565</c>)
    /// before dropping it, because the frontend DTO has no field for it.
    /// </summary>
    [Fact]
    public void SubmitDiasporaVerificationRequest_accepts_what_the_wizard_collects()
    {
        string[] frontend =
        [
            "nationality", "residenceCountry", "documentType",
            "issueCountry", "issueDate", "expiryDate", "supportingNotes"
        ];

        var backend = WireNamesOf<SubmitDiasporaVerificationRequest>().ToList();

        backend.Should().Contain(frontend);
        backend.Should().Contain("documentNumber", "the wizard collects it and the reviewer needs it");
        backend.Should().Contain("documentReference", "renamed from documentUri — it is a storage key");
    }

    /// <summary>
    /// <c>src/app/profile/appeals.tsx:40</c> posts <c>{ type, targetId, reason }</c>. Those
    /// three must be accepted unchanged, which is what lets one endpoint replace the two the
    /// mocks declare.
    /// </summary>
    [Fact]
    public void SubmitAppealRequest_accepts_the_frontends_inline_body_unchanged()
    {
        var backend = WireNamesOf<SubmitAppealRequest>().ToList();

        backend.Should().Contain(["type", "targetId", "reason"]);

        // From the frontend's separate AppealSubmissionRequest type (verification.ts:94),
        // carried as optional so the diaspora appeal maps here too.
        backend.Should().Contain(["additionalDocuments", "correctionRequest"]);
    }

    /// <summary><c>src/types/verification.ts:45</c>.</summary>
    [Fact]
    public void SubmitCountryVerificationRequest_matches_VerificationSubmitRequest()
    {
        string[] frontend = ["countryCode", "documentType", "arrivalDate", "expertiseAreas"];

        WireNamesOf<SubmitCountryVerificationRequest>().Should().Contain(frontend);
    }

    // ── Governance thresholds ────────────────────────────────────────────

    /// <summary>
    /// <c>src/lib/trustEngine.ts:7</c> — <c>GOVERNANCE_CONFIG</c>.
    /// <para>
    /// These thresholds currently live on the device, where any member with a debugger can
    /// edit them. Moving enforcement to the server must not change anyone's experience on the
    /// day it ships, so the server's defaults are asserted equal to the client's. Tightening
    /// them later is a deliberate decision, not a side effect of relocating the check.
    /// </para>
    /// </summary>
    [Theory]
    // 15 Sep 2026: a verified account is enough to publish a guide, list a home or sell an item,
    // and the Guides, Housing and Marketplace services check that. Changed on both sides at once.
    [InlineData(TrustCapabilities.PublishGuide, 0, 0, false, false, false)]
    // 14 Sep 2026: a verified account is enough, and the Jobs service checks that. Changed on
    // both sides in the same change, which is what this theory exists to hold.
    [InlineData(TrustCapabilities.PostJob, 0, 0, false, false, false)]
    [InlineData(TrustCapabilities.ListHousing, 0, 0, false, false, false)]
    [InlineData(TrustCapabilities.SellItem, 0, 0, false, false, false)]
    [InlineData(TrustCapabilities.Comment, 10, 0, true, false, false)]
    public void Capability_thresholds_match_the_client_governance_config(
        string capability,
        int minimumReputation,
        int minimumYears,
        bool requiresPhone,
        bool requiresIdentity,
        bool requiresCountry)
    {
        var requirement = DefaultTrustRequirements.All
            .Single(item => item.CapabilityKey == capability);

        requirement.MinimumCountryReputation.Should().Be(minimumReputation);
        requirement.MinimumResidenceYears.Should().Be(minimumYears);
        requirement.RequiresPhone.Should().Be(requiresPhone);
        requirement.RequiresIdentity.Should().Be(requiresIdentity);
        requirement.RequiresCountryVerification.Should().Be(requiresCountry);
    }

    /// <summary>
    /// <c>src/lib/trustEngine.ts:161</c> — the per-action rate limits and cooldowns.
    /// </summary>
    [Theory]
    [InlineData(TrustCapabilities.PublishGuide, 3, 60)]
    [InlineData(TrustCapabilities.Comment, 30, 5)]
    [InlineData(TrustCapabilities.PublishUpdate, 10, 30)]
    [InlineData(TrustCapabilities.PostJob, 5, 10)]
    public void Rate_limits_match_the_client_trust_engine(
        string capability, int perDay, int cooldownSeconds)
    {
        var requirement = DefaultTrustRequirements.All
            .Single(item => item.CapabilityKey == capability);

        requirement.RateLimitPerDay.Should().Be(perDay);
        requirement.CooldownSeconds.Should().Be(cooldownSeconds);
    }

    /// <summary>
    /// <c>src/config/featureRegistry.ts</c> and the trust centre's Feature Gates tab key off
    /// these ids. They are the frontend's <c>featureId</c> values verbatim.
    /// </summary>
    [Fact]
    public void Capability_keys_match_the_frontend_feature_ids()
    {
        var keys = DefaultTrustRequirements.All.Select(item => item.CapabilityKey).ToList();

        keys.Should().Contain(["guides", "jobs", "housing", "marketplace"]);
    }

    /// <summary>
    /// The display names the Feature Gates tab renders, from
    /// <c>getFeatureUnlockRequirements</c> (<c>trustEngine.ts:383</c>).
    /// </summary>
    [Fact]
    public void Capability_display_names_match_the_frontend()
    {
        var names = DefaultTrustRequirements.All.ToDictionary(
            item => item.CapabilityKey, item => item.Name, StringComparer.Ordinal);

        names["guides"].Should().Be("Guide Publishing");
        names["jobs"].Should().Be("Job Posting");
        names["housing"].Should().Be("Housing Listing");
        names["marketplace"].Should().Be("Marketplace Directory Selling");
    }
}
