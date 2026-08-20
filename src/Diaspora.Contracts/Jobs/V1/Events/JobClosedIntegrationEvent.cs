using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Jobs.V1.Events;

/// <summary>
/// A job posting stopped accepting applications.
/// <para>
/// <b>The de-indexing signal.</b> A closed posting left in a search index is a candidate
/// spending an afternoon on an application nobody will read, which is the single most corrosive
/// thing a job board can do to the people using it.
/// </para>
/// <para>
/// <see cref="Reason"/> distinguishes the three ways a posting closes, and they mean different
/// things to a candidate with a live application: <c>filled</c> says someone got it,
/// <c>expired</c> says the window passed, <c>withdrawn</c> says the employer pulled it. All three
/// are worth telling an applicant; none of them is "your application was rejected".
/// </para>
/// </summary>
public sealed record JobClosedIntegrationEvent : IntegrationEvent
{
    public required Guid JobId { get; init; }

    public required Guid EmployerId { get; init; }

    /// <summary><c>filled</c>, <c>expired</c> or <c>withdrawn</c>.</summary>
    public required string Reason { get; init; }

    /// <summary>What the employer told applicants, when they said anything.</summary>
    public string? Message { get; init; }

    /// <summary>Null when the clock closed it rather than a person.</summary>
    public Guid? ClosedBy { get; init; }

    public required DateTimeOffset ClosedAt { get; init; }

    /// <summary>
    /// The employer's notifiable member — B13.1. The employer's current primary owner.
    /// <para>
    /// <b>Carried for the <c>expired</c> case above all.</b> A posting that lapsed was closed by
    /// the clock, not by anybody at the employer, and an employer who discovers weeks later that
    /// their vacancy stopped being advertised has been failed by the platform. The other two
    /// reasons are somebody's own action and warrant nothing.
    /// </para>
    /// </summary>
    public Guid? EmployerUserId { get; init; }
}
