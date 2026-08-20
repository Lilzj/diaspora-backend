using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Jobs.V1.Events;

/// <summary>
/// A candidate applied to a job.
/// <para>
/// The event that notifies the recruiter, starts the employer's response clock and increments
/// the funnel. Four consumers of one fact.
/// </para>
/// <para>
/// <b>It carries no part of the candidate's profile, and that is deliberate.</b> An application
/// is personal data — a real name attached to a job hunt, often conducted while employed — and
/// spraying it across the platform's message bus would leave copies in outbox rows and consumer
/// logs the candidate never agreed to. A consumer that legitimately needs the candidate reads
/// them through the module's contracts, under the visibility rules.
/// </para>
/// </summary>
public sealed record ApplicationSubmittedIntegrationEvent : IntegrationEvent
{
    public required Guid ApplicationId { get; init; }

    public required Guid JobId { get; init; }

    public required Guid EmployerId { get; init; }

    public required Guid CandidateId { get; init; }

    /// <summary>The applying member, so a notification can be addressed. Nothing more.</summary>
    public required Guid ApplicantUserId { get; init; }

    public required DateTimeOffset SubmittedAt { get; init; }

    /// <summary>
    /// The employer's notifiable member — B13.1.
    /// <para>
    /// <b>The field that makes "you have a new application" possible.</b> Before it, the only
    /// message this contract could produce was the acknowledgement to the candidate, because the
    /// employer is an organisation and the payload named nobody at it. Resolving that downstream
    /// would have meant Communication querying Jobs, which is the coupling this platform does
    /// not have.
    /// </para>
    /// <para>
    /// One named person — the employer's current primary owner — not the recruiting team. A
    /// fan-out to every member of an employer with forty recruiters is a decision for the module
    /// that knows what a recruiting team is, and this is not it.
    /// </para>
    /// <para>
    /// Null on events published before B13.1, and null when the employer could not be read at
    /// publication. A consumer that finds it null sends nothing and that is the correct outcome.
    /// </para>
    /// </summary>
    public Guid? EmployerUserId { get; init; }

    /// <summary>
    /// The posting's category key — <c>software-engineering</c>, <c>hospitality</c>. Added in
    /// PSR-1.
    /// <para>
    /// <b>An attribute of the advertisement, not of the applicant.</b> The paragraph above still
    /// holds and this does not weaken it: a category is what the employer published to the world,
    /// and carrying it says nothing about who applied. What it makes possible is an affinity for
    /// the kind of work rather than for one posting — B14 records an application as its strongest
    /// behavioural signal and could only attach it to a job identifier, so a member who applied to
    /// three plumbing jobs was never observed to be interested in plumbing.
    /// </para>
    /// <para>
    /// Marketplace's sale contract has carried <c>CategoryKey</c> and <c>CountryCode</c> since
    /// B10 for exactly this purpose; this is the same two fields, on the same kind of event, for
    /// the same consumer.
    /// </para>
    /// <para>
    /// Null on events published before PSR-1. A consumer that finds it null records the signal
    /// without an interest key, which is what every consumer did until now.
    /// </para>
    /// </summary>
    public string? CategoryKey { get; init; }

    /// <summary>
    /// The posting's country. Added in PSR-1 beside <see cref="CategoryKey"/>, so a signal can
    /// carry the context a recommendation is scoped by. Null on pre-enrichment payloads.
    /// </summary>
    public string? CountryCode { get; init; }
}
