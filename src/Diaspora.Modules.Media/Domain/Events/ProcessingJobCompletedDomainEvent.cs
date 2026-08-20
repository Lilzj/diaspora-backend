using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Events;

/// <summary>
/// Raised when a processing job closes, however it closed.
/// <para>
/// Carries the unsupported count separately from the failed count so a subscriber — a metric,
/// an alert, a readiness probe — can tell a deployment missing a codec from a deployment whose
/// codec is broken. Those two need different responses and the first should not page anyone.
/// </para>
/// </summary>
public sealed record ProcessingJobCompletedDomainEvent : DomainEvent
{
    public required ProcessingJobId JobId { get; init; }

    public required MediaId MediaId { get; init; }

    public required ProcessingJobStatus Status { get; init; }

    public required int FailedTaskCount { get; init; }

    public required int UnsupportedTaskCount { get; init; }
}
