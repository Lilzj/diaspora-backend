using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Dashboards;

/// <summary>What a job seeker sees when they open the platform.</summary>
[RequirePermission(JobPermissions.SavedManage)]
public sealed record GetCandidateDashboardQuery : IQuery<CandidateDashboardResponse>;
