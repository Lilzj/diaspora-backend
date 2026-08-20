using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Applications;

/// <summary>
/// Moves an application through the hiring workflow.
/// <para>
/// The employer's mutator. The candidate's own exit is <see cref="WithdrawApplicationCommand"/>
/// and is not reachable from here — it must not be issuable by an employer passing an enum value.
/// </para>
/// </summary>
[Authorize(Permission = JobPermissions.ApplicationsDecide)]
[Auditable]
[Transactional(Reason = "Moves the application and updates the employer's response metrics.")]
public sealed record DecideApplicationCommand : ICommand<JobApplicationResponse>
{
    public required Guid ApplicationId { get; init; }

    public required DecideApplicationRequest Request { get; init; }
}
