using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;

namespace Diaspora.Modules.Jobs.Application.Applications;

/// <summary>
/// The candidate withdraws.
/// <para>
/// Granted under restriction, deliberately: a restriction that trapped someone in a hiring
/// process they wanted out of would be the platform working against the person it restricted.
/// </para>
/// </summary>
[RequirePermission(JobPermissions.ApplicationWithdraw)]
[Auditable]
[Transactional(Reason = "Withdraws the application and corrects the posting's applicant count.")]
public sealed record WithdrawApplicationCommand : ICommand
{
    public required Guid ApplicationId { get; init; }

    public required WithdrawApplicationRequest Request { get; init; }
}
