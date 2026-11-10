using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Contracts.V1.Responses;

namespace Diaspora.Modules.Providers.Application.Reviews;

/// <summary>
/// Writes a member's review of a provider.
/// <para>
/// <c>[Transactional]</c> because the review and the provider's recomputed rating rollup must
/// land together — a review that saved while the summary did not is precisely the silent drift
/// that makes ratings untrustworthy.
/// </para>
/// </summary>
[Transactional]
[Auditable]
[Authorize(Policy = ProviderPolicies.Review)]
public sealed record WriteProviderReviewCommand : ICommand<ProviderReviewResponse>
{
    public required Guid ProviderId { get; init; }

    public required WriteProviderReviewRequest Request { get; init; }
}
