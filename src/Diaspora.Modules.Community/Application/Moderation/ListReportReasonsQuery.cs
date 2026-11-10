using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Responses;

namespace Diaspora.Modules.Community.Application.Moderation;

/// <summary>The reason catalogue a client builds its report modal from.</summary>
[Authorize(Policy = CommunityPolicies.Read)]
public sealed record ListReportReasonsQuery : IQuery<IReadOnlyList<ReportReasonResponse>>;
