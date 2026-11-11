using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Dashboards;

/// <summary>What a recruiter sees when they open the platform.</summary>
[RequirePermission(JobPermissions.ApplicationsReview)]
public sealed record GetEmployerDashboardQuery : IQuery<EmployerDashboardResponse>
{
    /// <summary>Omitted when the caller belongs to exactly one employer.</summary>
    public Guid? EmployerId { get; init; }
}
