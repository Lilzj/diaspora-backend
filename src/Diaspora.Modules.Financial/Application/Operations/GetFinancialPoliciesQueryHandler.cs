using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>Reads the policy catalogue.</summary>
public sealed class GetFinancialPoliciesQueryHandler
    : IQueryHandler<GetFinancialPoliciesQuery, IReadOnlyList<FinancialPolicyResponse>>
{
    private readonly IFinancialPolicyRepository _policies;

    public GetFinancialPoliciesQueryHandler(IFinancialPolicyRepository policies) =>
        _policies = policies;

    public async Task<Result<IReadOnlyList<FinancialPolicyResponse>>> Handle(
        GetFinancialPoliciesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var policies = query.PolicyKey is { } key
            ? await _policies.ListByKeyAsync(key, cancellationToken)
            : await _policies.ListActiveAsync(cancellationToken);

        var visible = query.IncludeInactive
            ? policies
            : [.. policies.Where(policy => policy.IsActive)];

        return Result.Success<IReadOnlyList<FinancialPolicyResponse>>(
            [.. visible.Select(FinancialMapper.ToResponse)]);
    }
}
