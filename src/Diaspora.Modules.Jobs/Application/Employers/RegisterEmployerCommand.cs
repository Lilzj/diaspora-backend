using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Employers;

/// <summary>
/// Registers an employer, with the caller as its first owner.
/// <para>
/// Gated at <see cref="JobPermissions.EmployerCreate"/>, whose floor is Verified: an employer
/// profile is a claim about a real organisation that candidates will act on.
/// </para>
/// <para>
/// Not <c>[Transactional]</c> — the handler writes one aggregate and saves once, which EF already
/// makes atomic.
/// </para>
/// </summary>
[RequirePermission(JobPermissions.EmployerCreate)]
[Auditable]
public sealed record RegisterEmployerCommand : ICommand<EmployerResponse>
{
    public required RegisterEmployerRequest Request { get; init; }
}
