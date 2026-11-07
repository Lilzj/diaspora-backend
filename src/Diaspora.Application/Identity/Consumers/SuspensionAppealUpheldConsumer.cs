using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Application.Identity.Consumers;

/// <summary>
/// Lifts a member's suspension when Trust upholds their appeal against it (7 Nov 2026).
/// <para>
/// Until now an upheld suspension appeal was recorded in Trust and changed nothing: the member stayed
/// suspended, told their appeal had succeeded. The appeal's target is the appellant's own account and
/// neither member of staff behind the suspension may review it (Trust checks both when it is filed),
/// so the approval is the decision; this carries it out, in the reviewer's name, and the member is
/// told their access is back by the access-change notice.
/// </para>
/// </summary>
public sealed class SuspensionAppealUpheldConsumer : IntegrationEventHandler<AppealApprovedIntegrationEvent>
{
    public const string AppealType = "Suspension";

    private readonly IUserRepository _users;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SuspensionAppealUpheldConsumer(
        IUserRepository users,
        ISecurityEventWriter securityEvents,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _securityEvents = securityEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        AppealApprovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!string.Equals(integrationEvent.AppealType, AppealType, StringComparison.Ordinal))
        {
            return;
        }

        var user = await _users.GetByIdAsync(UserId.From(integrationEvent.UserId), cancellationToken);

        // Already lifted — its date passed, or staff reinstated the member — or a second delivery.
        if (user is null || user.AccessState != AccessState.Suspended)
        {
            return;
        }

        var note = string.IsNullOrWhiteSpace(integrationEvent.Explanation)
            ? "Your appeal was upheld."
            : $"Your appeal was upheld: {integrationEvent.Explanation.Trim()}";

        var reinstatement = user.Reinstate(_clock.UtcNow, AccessStateChangeReason.ModerationReinstatement, note);
        if (reinstatement.IsFailure)
        {
            return;
        }

        _users.Update(user);

        var metadata = new Dictionary<string, string>
        {
            ["notes"] = note,
            ["appealId"] = integrationEvent.AppealId.ToString()
        };

        if (integrationEvent.DecidedByUserId is { } reviewer)
        {
            metadata["actedBy"] = reviewer.ToString();
        }

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            Type = SecurityEventType.AccountReinstated,
            Severity = SecurityEventSeverity.Notice,
            Description = $"Suspension lifted on appeal; reinstated to {user.AccessState.Name}.",
            Metadata = metadata
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
