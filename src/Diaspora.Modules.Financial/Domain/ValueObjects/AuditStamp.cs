using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.ValueObjects;

/// <summary>
/// Everything the brief requires an auditable financial action to carry, in one value.
/// <para>
/// Actor, source, reason, correlation id, request id and the policy version in force. Every ledger
/// entry carries one; a movement whose stamp is absent cannot be written, because the factory takes
/// it rather than defaulting it.
/// </para>
/// <para>
/// <b>The policy version is on the stamp rather than looked up later, and that is the field most
/// likely to be forgotten.</b> Commission was 7% when this entry was written; the catalogue now says
/// 5%. Recomputing a historical entry from today's policy produces a plausible, wrong number and
/// nothing anywhere reports a problem — which is the failure mode the platform's handbook names as
/// its most expensive recurring class.
/// </para>
/// </summary>
public sealed class AuditStamp : ValueObject
{
    private AuditStamp(
        FinancialActor actor,
        FinancialSource source,
        string reason,
        string? correlationId,
        string? requestId,
        string? policyVersion)
    {
        Actor = actor;
        Source = source;
        Reason = reason;
        CorrelationId = correlationId;
        RequestId = requestId;
        PolicyVersion = policyVersion;
    }

    private AuditStamp() { }

    public FinancialActor Actor { get; private set; } = default!;

    public FinancialSource Source { get; private set; }

    /// <summary>Why, in words a person reading a statement can use. Never empty.</summary>
    public string Reason { get; private set; } = string.Empty;

    public string? CorrelationId { get; private set; }

    public string? RequestId { get; private set; }

    /// <summary>
    /// The policy version in force when the amount was decided. Null only when no policy was
    /// consulted — a member-to-member transfer at face value.
    /// </summary>
    public string? PolicyVersion { get; private set; }

    public static AuditStamp Create(
        FinancialActor actor,
        FinancialSource source,
        string reason,
        string? correlationId = null,
        string? requestId = null,
        string? policyVersion = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new AuditStamp(
            actor,
            source,
            reason.Trim(),
            Trim(correlationId),
            Trim(requestId),
            Trim(policyVersion));
    }

    /// <summary>The same stamp with a policy version attached, for a two-step decision.</summary>
    public AuditStamp WithPolicyVersion(string? policyVersion) =>
        new(Actor.Copy(), Source, Reason, CorrelationId, RequestId, Trim(policyVersion));

    /// <summary>
    /// A fresh instance with the same values, actor included.
    /// <para>
    /// <b>Every owner of an audit stamp needs its own.</b> One posting writes a transaction and
    /// several ledger entries; handing all of them one instance makes EF track it against whichever
    /// owner it saw first and write nulls into the others' NOT NULL columns — which surfaces as
    /// <c>NOT NULL constraint failed: …AuditActorType</c> and names nothing that explains it. The
    /// platform's handbook records the same failure for a cached static value object, and a value
    /// threaded through a request has exactly the same shape.
    /// </para>
    /// </summary>
    public AuditStamp Copy() =>
        new(Actor.Copy(), Source, Reason, CorrelationId, RequestId, PolicyVersion);

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Actor;
        yield return Source;
        yield return Reason;
        yield return CorrelationId;
        yield return RequestId;
        yield return PolicyVersion;
    }
}
