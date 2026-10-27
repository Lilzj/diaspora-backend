using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Trust.Application.Consumers;

/// <summary>
/// Counts a verification a reviewer rejected as fraud-like toward the member's risk (RWD-14, CS-4b).
/// <para>
/// <b>Trust's own announcement, consumed as any module's is</b>, as <c>VerificationRevoked</c>
/// already is: the decision is committed, and its risk follows through the outbox rather than inside
/// the reviewer's request. Only <c>SUSPECTED_FRAUD</c>, <c>FACE_MATCH_FAILED</c> and
/// <c>DUPLICATE_SUBMISSION</c> count (step 3f, Q7); an honest rejection records nothing.
/// </para>
/// <para>
/// Upheld reports count through <see cref="ReportUpheldReputationConsumer"/>, because Trust already
/// consumes that event and one module owns one reaction to one published fact.
/// </para>
/// </summary>
public sealed class VerificationRejectedRiskConsumer
    : IntegrationEventHandler<VerificationRejectedIntegrationEvent>
{
    private readonly MemberRiskAssessor _risk;

    public VerificationRejectedRiskConsumer(MemberRiskAssessor risk) => _risk = risk;

    protected override Task HandleAsync(
        VerificationRejectedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var counts = integrationEvent.UserId != Guid.Empty
            && Enum.TryParse<VerificationRejectionReason>(integrationEvent.ReasonCode, out var reason)
            && RiskPolicy.CountsAgainstMember(reason)
            && Enum.TryParse<VerificationType>(integrationEvent.VerificationType, out _);

        return counts
            ? _risk.RecordAsync(
                RiskSignal.VerificationRejected(
                    UserId.From(integrationEvent.UserId),
                    integrationEvent.VerificationId,
                    Enum.Parse<VerificationType>(integrationEvent.VerificationType),
                    integrationEvent.RejectedAt),
                cancellationToken)
            : Task.CompletedTask;
    }
}
