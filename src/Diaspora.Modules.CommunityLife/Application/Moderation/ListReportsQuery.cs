using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Moderation;

/// <summary>The moderation queue. Staff only.</summary>
[Authorize(Permission = CommunityLifePermissions.Moderate)]
public sealed record ListReportsQuery : IQuery<ReportPageResponse>
{
    public string? SubjectType { get; init; }

    public IReadOnlyList<string>? Statuses { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
