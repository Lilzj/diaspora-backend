using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Applications;

/// <summary>
/// Submits an application.
/// <para>
/// Gated at <see cref="JobPermissions.Apply"/>, whose floor is Verified — the key and the floor
/// the host declared provisionally from B2 onwards, unchanged. An application puts a real name in
/// front of a stranger who will make a decision about someone's livelihood.
/// </para>
/// <para>
/// <c>[Transactional]</c>: the handler writes the application and nudges the posting's status and
/// counters. An application recorded against a posting that never learned about it would make
/// every dashboard figure wrong.
/// </para>
/// </summary>
[Authorize(Permission = JobPermissions.Apply)]
[Auditable]
[Transactional(Reason = "Writes the application and updates the posting it was sent to.")]
public sealed record ApplyToJobCommand : ICommand<JobApplicationResponse>
{
    public required Guid JobId { get; init; }

    public required ApplyToJobRequest Request { get; init; }
}
