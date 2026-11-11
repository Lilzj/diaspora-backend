using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Subscriptions;

/// <summary>The plan catalogue.</summary>
[RequirePermission(FinancialPermissions.SubscriptionsRead)]
public sealed record GetSubscriptionPlansQuery : IQuery<IReadOnlyList<SubscriptionPlanResponse>>
{
    public string? AudienceType { get; init; }

    public string? CountryCode { get; init; }
}
