using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Employers;

/// <summary>
/// Updates an employer profile.
/// <para>
/// The policy gates "may this caller manage employers at all"; the handler asks the aggregate
/// whether they are an administrator <em>of this one</em>. Both run.
/// </para>
/// </summary>
[Authorize(Permission = JobPermissions.EmployerManage)]
[Auditable]
public sealed record UpdateEmployerCommand : ICommand<EmployerResponse>
{
    public required Guid EmployerId { get; init; }

    public required UpdateEmployerRequest Request { get; init; }
}
