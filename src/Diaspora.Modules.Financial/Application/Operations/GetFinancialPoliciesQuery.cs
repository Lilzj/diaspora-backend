using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>
/// The policy catalogue.
/// <para>
/// Includes superseded versions when asked, because "what did we charge in March" is a question the
/// catalogue is the only place to answer.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PoliciesRead)]
public sealed record GetFinancialPoliciesQuery : IQuery<IReadOnlyList<FinancialPolicyResponse>>
{
    public string? PolicyKey { get; init; }

    public bool IncludeInactive { get; init; }
}
