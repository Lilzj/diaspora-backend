using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Jobs.V1.Events;

/// <summary>
/// A job posting was retired by its employer.
/// <para>
/// Distinct from closing: a closed posting is still readable and still tells a candidate what
/// happened to their application, whereas an archived one is off every public surface. Kept for
/// the record — the applications against it stay readable to both parties, because an employer's
/// hiring history and a candidate's own record must not vanish because an advert was tidied away.
/// </para>
/// </summary>
public sealed record JobArchivedIntegrationEvent : IntegrationEvent
{
    public required Guid JobId { get; init; }

    public required Guid EmployerId { get; init; }

    /// <summary>Null when archival was a side effect — an employer closing, a moderation action.</summary>
    public Guid? ArchivedBy { get; init; }

    public required DateTimeOffset ArchivedAt { get; init; }

    /// <summary>
    /// The employer's notifiable member — B13.1. The employer's current primary owner.
    /// <para>
    /// Carried for the same case <see cref="ArchivedBy"/> is null for: an archival that was a
    /// side effect or a moderation action is the one an employer did not perform and therefore
    /// the one they have to be told about.
    /// </para>
    /// </summary>
    public Guid? EmployerUserId { get; init; }
}
