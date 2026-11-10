using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>
/// Publishes a new version of a financial rule.
/// <para>
/// <b>Audited, because a policy change is invisible and enormous.</b> Raising marketplace commission
/// does not fail, does not log an error and does not change any response shape — every sale from that
/// moment simply pays the platform more. The audit entry and the published event are the only two
/// records that it happened.
/// </para>
/// <para>
/// <b>Transactional: publishing writes the new version and closes the previous one.</b> Both or
/// neither — two active versions of one key would make pricing non-deterministic, and none at all
/// would make it a refusal.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.PoliciesManage)]
[Transactional(Reason = "Publishing a version and superseding the previous one is one decision.")]
[Auditable(Action = "financial.policy.publish")]
public sealed record PublishFinancialPolicyCommand : ICommand<FinancialPolicyResponse>
{
    public required string PolicyKey { get; init; }

    public required string Kind { get; init; }

    public string? Scope { get; init; }

    public string? ScopeValue { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyDictionary<string, string> Parameters { get; init; }

    public DateTimeOffset? EffectiveFrom { get; init; }
}
