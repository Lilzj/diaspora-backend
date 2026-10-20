using Diaspora.Persistence;
using Diaspora.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Modules.CommunityProgramme.Persistence.Configurations;

/// <summary>
/// The module's table names and column widths, in one place.
/// <para>
/// Named here rather than inline so a migration and a query cannot disagree about which table a
/// thing lives in, and so renaming one is a single edit rather than a search.
/// </para>
/// </summary>
internal static class CommunityProgrammePersistenceConstants
{
    // ── Tables ──────────────────────────────────────────────────────────────────────────────

    // ── Communities — the groups people belong to, not the programmes organizations sell ──
    public const string CommunitiesTable = "communities";
    public const string CommunityMembershipsTable = "community_memberships";
    public const string CommunityDeletionRequestsTable = "community_deletion_requests";
    public const string CommunityOwnershipTransfersTable = "community_ownership_transfers";
    public const string CommunityRulesTable = "community_rules";
    public const string CommunityNoticesTable = "community_notices";
    public const string CommunityMemberPreferencesTable = "community_member_preferences";
    public const string CommunityInitiativesTable = "community_initiatives";
    public const string CommunityInitiativeParticipantsTable = "community_initiative_participants";
    public const string CommunityInitiativeUpdatesTable = "community_initiative_updates";

    public const string ProgrammesTable = "membership_programmes";
    public const string ProgrammeTiersTable = "membership_tiers";
    public const string TierBenefitsTable = "membership_tier_benefits";
    public const string TierRequirementsTable = "membership_tier_requirements";
    public const string MembershipsTable = "programme_memberships";
    public const string MembershipHistoryTable = "programme_membership_history";
    public const string OpportunitiesTable = "volunteer_opportunities";
    public const string ShiftsTable = "volunteer_shifts";
    public const string OpportunitySkillsTable = "volunteer_opportunity_skills";
    public const string ApplicationsTable = "volunteer_applications";
    public const string ApplicationAnswersTable = "volunteer_application_answers";
    public const string AssignmentsTable = "volunteer_assignments";
    public const string CertificatesTable = "volunteer_certificates";
    public const string PollsTable = "community_polls";
    public const string PollOptionsTable = "community_poll_options";
    public const string PollInviteesTable = "community_poll_invitees";
    public const string PollResponsesTable = "community_poll_responses";
    public const string PollResponseSelectionsTable = "community_poll_response_selections";
    public const string AnnouncementsTable = "community_announcements";
    public const string AnnouncementMediaTable = "community_announcement_media";

    // ── Widths ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Enum names are stored as strings, so a value added later reads correctly on old rows.</summary>
    public const int EnumLength = 32;

    /// <summary>A base64 SHA-256, which is 44 characters. Sixty-four leaves room for a longer hash.</summary>
    public const int FingerprintLength = 64;
}

/// <summary>
/// The module's identifier converters.
/// <para>
/// One helper rather than a converter type per identifier, exactly as every module since B7 does.
/// The generic converter is the platform's, so a strongly typed identifier persists as its Guid
/// everywhere on the platform and no module invents a second encoding.
/// </para>
/// </summary>
internal static class CommunityProgrammeValueConverters
{
    public static ValueConverter Id<TId>() where TId : StronglyTypedId =>
        new StronglyTypedIdValueConverter<TId>();
}
