using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when a candidate applies.
/// <para>
/// This is what starts the employer's response clock, notifies the recruiter, nudges the
/// posting into <c>ReceivingApplications</c> and increments the analytics funnel — four
/// consumers of one fact, which is why it is a domain event rather than four handler calls
/// inside the command.
/// </para>
/// </summary>
public sealed record JobApplicationSubmittedDomainEvent : DomainEvent
{
    public required JobApplicationId ApplicationId { get; init; }

    public required JobPostingId PostingId { get; init; }

    public required EmployerProfileId EmployerId { get; init; }

    public required CandidateProfileId CandidateId { get; init; }

    public required UserId ApplicantUserId { get; init; }

    public required DateTimeOffset SubmittedAt { get; init; }

    /// <summary>
    /// The posting's category key, stamped at raise time. Added in PSR-1.
    /// <para>
    /// <b>What the job was about, not who applied.</b> An application is personal data and this
    /// event deliberately carries none of it; a category is a public attribute of a public
    /// advertisement, and it is what lets a downstream consumer record an affinity for the
    /// <em>kind</em> of work rather than for one posting that will close.
    /// </para>
    /// <para>
    /// Free to carry: the command handler already holds the posting to check that it accepts
    /// applications, so this is the raise-time route rather than a read back.
    /// </para>
    /// </summary>
    public string? CategoryKey { get; init; }

    /// <summary>The posting's country, stamped at raise time. Added in PSR-1 with
    /// <see cref="CategoryKey"/>, for the same reason and from the same already-loaded posting.</summary>
    public string? CountryCode { get; init; }
}
