using Diaspora.Contracts.Authorization;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Volunteering;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityProgramme.Application.Services;

/// <summary>
/// Decides whether a caller may apply for a piece of volunteer work, and says why not when they may
/// not.
/// <para>
/// <b>Both halves are the server's, and the register asks for both by name:</b> <c>canApply</c> and
/// <c>cannotApplyReason</c>, "evaluated server-side and returned as a decision the client renders
/// rather than computes". The reason is a sentence rather than a code because the client prints it
/// verbatim, and a client composing its own explanation would be describing a decision it did not
/// make.
/// </para>
/// <para>
/// <b>A full opportunity does not refuse.</b> It waitlists — the kinder of the two correct answers,
/// and the register's own instruction — so being full is not among the reasons below. What decides
/// between <c>Pending</c> and <c>Waitlisted</c> is the application handler, from the counters.
/// </para>
/// </summary>
public sealed class VolunteerEligibilityService
{
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public VolunteerEligibilityService(ICurrentUser currentUser, IClock clock)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <summary>What an organizer's own view says instead of an eligibility answer.</summary>
    public static string OrganizerCannotApply => "You manage this opportunity.";

    /// <summary>
    /// Why the caller cannot apply, or null when they can.
    /// <para>
    /// Ordered so the most actionable reason wins: "you have already applied" is more useful than
    /// "this closed", and both are more useful than "sign in".
    /// </para>
    /// </summary>
    public string? CannotApplyReason(
        VolunteerOpportunity opportunity,
        VolunteerApplication? existing,
        UserId? caller)
    {
        ArgumentNullException.ThrowIfNull(opportunity);

        if (caller is null)
        {
            return "Sign in to volunteer.";
        }

        if (existing is not null && !existing.IsTerminal)
        {
            return existing.Status switch
            {
                VolunteerApplicationStatus.Waitlisted => "You are on the waiting list.",
                VolunteerApplicationStatus.Approved => "You are already taking part.",
                _ => "You have already applied."
            };
        }

        if (!opportunity.AcceptsApplications)
        {
            return opportunity.Status switch
            {
                VolunteerOpportunityStatus.Draft => "This opportunity has not opened yet.",
                VolunteerOpportunityStatus.Cancelled => "This opportunity was cancelled.",
                VolunteerOpportunityStatus.Completed => "This opportunity has finished.",
                _ => "This opportunity has closed to new applications."
            };
        }

        if (opportunity.ApplicationDeadline is { } deadline && deadline <= _clock.UtcNow)
        {
            return "The deadline for applying has passed.";
        }

        // A minimum age is a fact about a member the platform does not hold: nothing on this platform
        // stores or verifies a date of birth. Rather than assume, the organizer is told to check —
        // which is what the requirement was always going to mean in practice, and which is honest
        // about what the server knows.
        if (opportunity.MinimumAge is { } age && CallerTier < AccessTier.Verified)
        {
            return $"This opportunity is for volunteers aged {age} and over, and asks for a "
                + "verified account.";
        }

        return null;
    }

    private AccessTier CallerTier =>
        _currentUser.Claims.TryGetValue(PlatformClaimTypes.AccessTier, out var raw)
            && Enum.TryParse<AccessTier>(raw, ignoreCase: true, out var tier)
                ? tier
                : AccessTier.Anonymous;
}
