using Diaspora.Platform.Services.Abstractions;
using Diaspora.Modules.CommunityLife.Integration;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Announcements;
using Diaspora.Modules.CommunityProgramme.Domain.Capabilities;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Modules.CommunityProgramme.Domain.Polls;
using Diaspora.Modules.CommunityProgramme.Domain.ValueObjects;
using Diaspora.Modules.CommunityProgramme.Domain.Volunteering;

namespace Diaspora.Modules.CommunityProgramme.Application.Mapping;

/// <summary>
/// The one place a domain object becomes a wire contract.
/// <para>
/// <b>Static, because none of it needs state</b>, and in one class so two handlers rendering the same
/// aggregate cannot disagree about what a field means. Everything that <em>does</em> need state — the
/// caller's capabilities, whether a requirement is met, where somebody sits on a waiting list — is
/// passed in as an already-decided value rather than resolved here, so the mapper cannot quietly
/// start making authorization decisions.
/// </para>
/// </summary>
public static class CommunityProgrammeMapper
{
    // ── Money ───────────────────────────────────────────────────────────────────────────────

    public static MoneyResponse? ToResponse(ProgrammePrice? price) =>
        price is null
            ? null
            : new MoneyResponse
            {
                Amount = price.Amount,
                Currency = price.Currency,
                Exponent = price.Exponent
            };

    // ── Capabilities ────────────────────────────────────────────────────────────────────────

    public static OrganizationCapabilitiesResponse ToResponse(
        OrganizationCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        return new OrganizationCapabilitiesResponse
        {
            OrganizationId = capabilities.OrganizationId,
            Role = capabilities.Role,
            CanManageMembers = capabilities.CanManageMembers,
            CanApproveMembers = capabilities.CanApproveMembers,
            CanManageTiers = capabilities.CanManageTiers,
            CanManageEvents = capabilities.CanManageEvents,
            CanManageCampaigns = capabilities.CanManageCampaigns,
            CanManageVolunteers = capabilities.CanManageVolunteers,
            CanManagePolls = capabilities.CanManagePolls,
            CanPublishAnnouncements = capabilities.CanPublishAnnouncements,
            CanViewAnalytics = capabilities.CanViewAnalytics,
            CanViewFinancials = capabilities.CanViewFinancials
        };
    }

    // ── Membership programme ────────────────────────────────────────────────────────────────

    public static MembershipBenefitResponse ToResponse(TierBenefit benefit)
    {
        ArgumentNullException.ThrowIfNull(benefit);

        return new MembershipBenefitResponse
        {
            Id = benefit.Id,
            Title = benefit.Title,
            Description = benefit.Description,
            Icon = benefit.Icon,
            IsActive = benefit.IsActive
        };
    }

    /// <summary>
    /// A requirement with the server's evaluation attached.
    /// <para>
    /// <paramref name="isMet"/> is passed in rather than computed, because it depends on the caller
    /// and a mapper that knew about the caller would be a mapper making an authorization decision.
    /// </para>
    /// </summary>
    public static MembershipRequirementResponse ToResponse(
        TierRequirement requirement, bool isMet)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        return new MembershipRequirementResponse
        {
            Id = requirement.Id,
            Label = requirement.Label,
            Description = requirement.Description,
            Kind = requirement.Kind.ToString(),
            IsMet = isMet,
            ActionRoute = requirement.ActionRoute
        };
    }

    public static MembershipTierResponse ToResponse(
        MembershipTier tier,
        Guid organizationId,
        Func<TierRequirement, bool> isMet)
    {
        ArgumentNullException.ThrowIfNull(tier);
        ArgumentNullException.ThrowIfNull(isMet);

        return new MembershipTierResponse
        {
            TierId = tier.Id.Value,
            OrganizationId = organizationId,
            Name = tier.Name,
            Description = tier.Description,
            Price = ToResponse(tier.Price),
            BillingPeriod = tier.BillingPeriod.ToString(),
            Benefits = [.. tier.Benefits.Select(ToResponse)],
            Requirements =
            [
                .. tier.Requirements.Select(requirement =>
                    ToResponse(requirement, isMet(requirement)))
            ],
            Capacity = tier.Capacity,
            FilledCount = tier.FilledCount,
            IsFull = tier.IsFull,
            IsRecommended = tier.IsRecommended,
            SortOrder = tier.SortOrder
        };
    }

    public static MembershipProgrammeResponse ToResponse(
        MembershipProgramme programme,
        OrganizationSummary organization,
        Func<TierRequirement, bool> isMet,
        ProgrammeMembershipStatus myStatus,
        Guid? myMembershipId)
    {
        ArgumentNullException.ThrowIfNull(programme);
        ArgumentNullException.ThrowIfNull(organization);

        return new MembershipProgrammeResponse
        {
            OrganizationId = programme.OrganizationId,
            OrganizationName = organization.Name,
            OrganizationLogoMediaId = organization.LogoMediaId,
            Category = organization.OrganizationTypeKey,
            IsVerified = organization.IsVerified,
            Visibility = programme.Visibility.ToString(),
            IsOpen = programme.IsOpen,
            RequiresApproval = programme.RequiresApproval,
            Summary = programme.Summary,
            Tiers =
            [
                .. programme.Tiers.Select(tier =>
                    ToResponse(tier, programme.OrganizationId, isMet))
            ],
            MemberCount = programme.MemberCount,
            CountryCode = organization.CountryCode,
            CityName = organization.CityName,
            MyStatus = myStatus.ToString(),
            MyMembershipId = myMembershipId
        };
    }

    // ── Membership ──────────────────────────────────────────────────────────────────────────

    public static MembershipHistoryEntryResponse ToResponse(MembershipHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new MembershipHistoryEntryResponse
        {
            Id = entry.Id,
            Status = entry.Status.ToString(),
            OccurredAt = entry.OccurredAt,
            Actor = entry.Actor.ToString(),
            Reason = entry.Reason
        };
    }

    public static ProgrammeMembershipResponse ToResponse(
        ProgrammeMembership membership,
        MembershipTier? tier,
        OrganizationSummary? organization)
    {
        ArgumentNullException.ThrowIfNull(membership);

        return new ProgrammeMembershipResponse
        {
            MembershipId = membership.Id.Value,
            OrganizationId = membership.OrganizationId,

            // A tier or an organization that no longer resolves is an ordinary state, not an
            // exceptional one: a body can be archived while somebody still holds a membership of it,
            // and the member is entitled to see what they hold either way.
            OrganizationName = organization?.Name ?? "Unknown organization",
            OrganizationLogoMediaId = organization?.LogoMediaId,
            TierId = membership.TierId.Value,
            TierName = tier?.Name ?? "Membership",
            Status = membership.Status.ToString(),
            Price = ToResponse(tier?.Price),
            BillingPeriod = (tier?.BillingPeriod ?? BillingPeriod.OneOff).ToString(),
            Benefits = tier is null ? [] : [.. tier.Benefits.Select(ToResponse)],
            JoinedAt = membership.JoinedAt,
            ExpiresAt = membership.ExpiresAt,
            RenewalDueAt = membership.RenewalDueAt,
            IsRenewable = membership.IsRenewable,
            GrantsBenefits = membership.GrantsBenefits,
            PaymentIntentId = membership.PaymentIntentId,
            History = [.. membership.History.Select(ToResponse)],
            DecisionReason = membership.DecisionReason
        };
    }

    /// <summary>
    /// A member as their organization's roll sees them.
    /// <para>
    /// Note what is not here: no display name, no avatar, no contact of any kind. The roll carries
    /// the identifiers and the standing; a client that wants names asks Identity for them with its
    /// own authorization, which is the boundary §45 draws and the reason this module never caches a
    /// member's name.
    /// </para>
    /// </summary>
    public static OrganizationMemberResponse ToRollResponse(
        ProgrammeMembership membership,
        MembershipTier? tier,
        OrganizationMembershipSummary? organizationMembership,
        IReadOnlyDictionary<Guid, MemberProfile> names) =>
        new()
        {
            MembershipId = membership.Id.Value,
            UserId = membership.MemberUserId.Value,
            DisplayName = NameOf(names, membership.MemberUserId.Value),
            TierId = membership.TierId.Value,
            TierName = tier?.Name ?? "Membership",
            Status = membership.Status.ToString(),
            Role = organizationMembership?.Role,
            OrganizationMembershipId = organizationMembership?.MembershipId,
            JoinedAt = membership.JoinedAt,
            ExpiresAt = membership.ExpiresAt,
            AppliedAt = membership.CreatedAt
        };

    /// <summary>
    /// A resolved name, or null. Never the identifier: a caller that cannot be told who somebody
    /// is has to say so, and the screen has a neutral label for exactly that.
    /// </summary>
    private static string? NameOf(IReadOnlyDictionary<Guid, MemberProfile> names, Guid userId) =>
        names.TryGetValue(userId, out var profile) ? profile.DisplayName : null;

    // ── Volunteering ────────────────────────────────────────────────────────────────────────

    public static VolunteerShiftResponse ToResponse(VolunteerShift shift, Guid opportunityId)
    {
        ArgumentNullException.ThrowIfNull(shift);

        return new VolunteerShiftResponse
        {
            ShiftId = shift.Id.Value,
            OpportunityId = opportunityId,
            Label = shift.Label,
            StartsAt = shift.StartsAt,
            EndsAt = shift.EndsAt,
            Capacity = shift.Capacity,
            FilledCount = shift.FilledCount,
            IsFull = shift.IsFull
        };
    }

    public static VolunteerOpportunityResponse ToResponse(
        VolunteerOpportunity opportunity,
        OrganizationSummary? organization,
        VolunteerApplication? myApplication,
        bool canApply,
        string? cannotApplyReason,
        bool canManage)
    {
        ArgumentNullException.ThrowIfNull(opportunity);

        return new VolunteerOpportunityResponse
        {
            OpportunityId = opportunity.Id.Value,
            Title = opportunity.Title,
            Description = opportunity.Description,
            Cause = opportunity.Cause,
            Status = opportunity.Status.ToString(),
            OrganizationId = opportunity.OrganizationId,
            OrganizationName = organization?.Name ?? "Unknown organization",
            OrganizationLogoMediaId = organization?.LogoMediaId,
            IsVerified = organization?.IsVerified ?? false,
            CountryCode = opportunity.CountryCode,
            CityName = opportunity.CityName,
            AreaName = opportunity.AreaName,
            AddressLine = opportunity.AddressLine,
            StartsAt = opportunity.StartsAt,
            EndsAt = opportunity.EndsAt,
            Shifts =
            [
                .. opportunity.Shifts.Select(shift =>
                    ToResponse(shift, opportunity.Id.Value))
            ],
            RequiredSkills = opportunity.RequiredSkills,
            MinimumAge = opportunity.MinimumAge,
            Capacity = opportunity.Capacity,
            FilledCount = opportunity.FilledCount,
            WaitlistCount = opportunity.WaitlistCount,
            IsFull = opportunity.IsFull,
            ApplicationDeadline = opportunity.ApplicationDeadline,
            MyApplicationStatus =
                (myApplication?.Status ?? VolunteerApplicationStatus.NotApplied).ToString(),
            MyApplicationId = myApplication?.Id.Value,
            CanApply = canApply,
            CannotApplyReason = cannotApplyReason,
            CanManage = canManage,
            CreatedAt = opportunity.CreatedAt
        };
    }

    public static VolunteerApplicationResponse ToResponse(
        VolunteerApplication application,
        VolunteerOpportunity? opportunity,
        OrganizationSummary? organization,
        int? waitlistPosition,
        VolunteerAssignmentResponse? assignment)
    {
        ArgumentNullException.ThrowIfNull(application);

        return new VolunteerApplicationResponse
        {
            ApplicationId = application.Id.Value,
            OpportunityId = application.OpportunityId.Value,
            OpportunityTitle = opportunity?.Title ?? "Volunteer opportunity",
            OrganizationId = application.OrganizationId,
            OrganizationName = organization?.Name ?? "Unknown organization",
            ShiftId = application.ShiftId?.Value,
            Status = application.Status.ToString(),
            Answers = ToAnswers(application),
            SubmittedAt = application.SubmittedAt,
            DecidedAt = application.DecidedAt,
            DecisionReason = application.DecisionReason,
            WaitlistPosition = waitlistPosition,
            Assignment = assignment
        };
    }

    public static VolunteerApplicantResponse ToApplicantResponse(
        VolunteerApplication application,
        int? waitlistPosition,
        IReadOnlyDictionary<Guid, MemberProfile> names)
    {
        ArgumentNullException.ThrowIfNull(application);

        return new VolunteerApplicantResponse
        {
            ApplicationId = application.Id.Value,
            UserId = application.VolunteerUserId.Value,
            DisplayName = NameOf(names, application.VolunteerUserId.Value),
            Status = application.Status.ToString(),
            Answers = ToAnswers(application),
            ShiftId = application.ShiftId?.Value,
            SubmittedAt = application.SubmittedAt ?? application.CreatedAt,
            WaitlistPosition = waitlistPosition
        };
    }

    public static VolunteerAssignmentResponse ToResponse(
        VolunteerAssignment assignment,
        VolunteerOpportunity? opportunity)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var shift = assignment.ShiftId is { } shiftId
            ? opportunity?.FindShift(shiftId)
            : null;

        return new VolunteerAssignmentResponse
        {
            AssignmentId = assignment.Id.Value,
            ApplicationId = assignment.ApplicationId.Value,
            OpportunityId = assignment.OpportunityId.Value,
            OpportunityTitle = opportunity?.Title ?? "Volunteer opportunity",
            OrganizationId = assignment.OrganizationId,
            VolunteerUserId = assignment.VolunteerUserId.Value,
            ShiftId = assignment.ShiftId?.Value,
            ShiftLabel = shift?.Label,
            StartsAt = assignment.StartsAt,
            EndsAt = assignment.EndsAt,
            Status = assignment.Status.ToString(),
            CheckedInAt = assignment.CheckedInAt,
            CheckedOutAt = assignment.CheckedOutAt,
            ConfirmedHours = assignment.ConfirmedHours,
            ConfirmedAt = assignment.ConfirmedAt
        };
    }

    public static VolunteerCertificateResponse ToResponse(
        VolunteerCertificate certificate, OrganizationSummary? organization)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        return new VolunteerCertificateResponse
        {
            CertificateId = certificate.Id.Value,
            Title = certificate.Title,
            OrganizationId = certificate.OrganizationId,
            OrganizationName = organization?.Name ?? "Unknown organization",
            Hours = certificate.Hours,

            // Null, and honestly so. See the response type's own documentation.
            DownloadUrl = null,
            IssuedAt = certificate.IssuedAt
        };
    }

    // ── Polls ───────────────────────────────────────────────────────────────────────────────

    public static PollOptionResponse ToResponse(PollOption option)
    {
        ArgumentNullException.ThrowIfNull(option);

        return new PollOptionResponse
        {
            OptionId = option.Id.Value,
            Label = option.Label,
            Description = option.Description,
            SortOrder = option.SortOrder
        };
    }

    public static CommunityPollResponse ToResponse(
        CommunityPoll poll,
        OrganizationSummary? organization,
        PollResponse? myResponse,
        string? cannotVoteReason,
        bool canManage)
    {
        ArgumentNullException.ThrowIfNull(poll);

        return new CommunityPollResponse
        {
            PollId = poll.Id.Value,
            Question = poll.Question,
            Description = poll.Description,
            Status = poll.Status.ToString(),
            VotingType = poll.VotingType.ToString(),
            Audience = poll.Audience.ToString(),
            Options = [.. poll.Options.Select(ToResponse)],
            MaxSelections = poll.MaxSelections,
            IsAnonymous = poll.IsAnonymous,
            AllowsChange = poll.AllowsChange,
            OrganizationId = poll.OrganizationId,
            OrganizationName = organization?.Name,
            OpensAt = poll.OpensAt,
            ClosesAt = poll.ClosesAt,
            TotalResponses = poll.TotalResponses,
            HasVoted = myResponse is not null,

            // Absent on an anonymous poll, structurally: there is no stored identity to find the
            // caller's own answer by, and inventing one would be worse than the absence.
            MyResponse = myResponse is null || poll.IsAnonymous
                ? null
                : ToResponse(myResponse),
            CanVote = cannotVoteReason is null,
            CannotVoteReason = cannotVoteReason,
            CanManage = canManage,
            CreatedAt = poll.CreatedAt
        };
    }

    public static PollResponseDetail ToResponse(PollResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new PollResponseDetail
        {
            ResponseId = response.Id.Value,
            PollId = response.PollId.Value,
            SelectedOptionIds =
                [.. response.Selections.Select(selection => selection.OptionId.Value)],
            OpenResponse = response.OpenResponse,
            RespondedAt = response.RespondedAt
        };
    }

    // ── Announcements ───────────────────────────────────────────────────────────────────────

    public static CommunityAnnouncementResponse ToResponse(
        CommunityAnnouncement announcement, string scopeName)
    {
        ArgumentNullException.ThrowIfNull(announcement);

        return new CommunityAnnouncementResponse
        {
            AnnouncementId = announcement.Id.Value,
            Scope = announcement.Scope.ToString(),
            ScopeId = announcement.ScopeId,
            ScopeName = scopeName,
            Title = announcement.Title,
            Body = announcement.Body,
            MediaIds = [.. announcement.Media.Select(media => media.MediaId)],
            PublishedByUserId = announcement.PublishedByUserId.Value,
            OrganizationId = announcement.OrganizationId,
            IsPinned = announcement.IsPinned,
            PublishedAt = announcement.PublishedAt,
            ActionRoute = ActionRouteFor(announcement)
        };
    }

    /// <summary>
    /// Where the platform inbox deep-links an announcement to.
    /// <para>
    /// Composed here rather than on the client, so a route that changes changes in one place — the
    /// same reason B13's notification contracts carry <c>targetRoute</c> rather than a type the
    /// client maps.
    /// </para>
    /// </summary>
    private static string? ActionRouteFor(CommunityAnnouncement announcement) =>
        announcement.Scope switch
        {
            AnnouncementScope.Organization when announcement.ScopeId is { } id =>
                $"/community-life/organizations/{id}",

            AnnouncementScope.Event when announcement.ScopeId is { } id =>
                $"/community-life/events/{id}",

            AnnouncementScope.Neighborhood when announcement.ScopeId is { } id =>
                $"/community-life/neighborhoods/{id}",

            AnnouncementScope.Campaign when announcement.ScopeId is { } id =>
                $"/community-life/campaign/{id}",

            _ => "/community-life/announcements"
        };

    private static Dictionary<string, string> ToAnswers(VolunteerApplication application)
    {
        var answers = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var answer in application.Answers)
        {
            answers[answer.Question] = answer.Answer;
        }

        return answers;
    }
}
