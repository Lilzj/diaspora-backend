using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised the first time someone at the employer opens an application.
/// <para>
/// <b>Once per application, not once per view.</b> A recruiter refreshing a list must not
/// produce a thousand events, and the only fact worth recording is that a human finally looked
/// — which is also the honest start of the employer's response clock, earlier and truer than
/// the first status change.
/// </para>
/// <para>
/// Whether the candidate is shown this is a product decision made downstream. It is a fact
/// worth having either way: an application that has sat unopened for three weeks is exactly
/// what the platform should be telling somebody about.
/// </para>
/// </summary>
public sealed record JobApplicationViewedDomainEvent : DomainEvent
{
    public required JobApplicationId ApplicationId { get; init; }

    public required JobPostingId PostingId { get; init; }

    public required EmployerProfileId EmployerId { get; init; }

    public required UserId ViewedBy { get; init; }

    /// <summary>How long the candidate waited for a human to open it.</summary>
    public required TimeSpan TimeToFirstView { get; init; }

    public required DateTimeOffset ViewedAt { get; init; }
}
