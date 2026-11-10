using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Contracts.V1.Responses;

namespace Diaspora.Modules.Providers.Application.Reviews;

/// <summary>A page of a provider's reviews, newest first, with the rollup attached.</summary>
[Authorize(Policy = ProviderPolicies.Read)]
public sealed record ListProviderReviewsQuery : IQuery<ProviderReviewPageResponse>
{
    public required Guid ProviderId { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
