using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.Lifecycle.ReinstateAccount;

public sealed class ReinstateAccountCommandHandler
    : ICommandHandler<ReinstateAccountCommand, OperationAcknowledgedResponse>
{
    private readonly IUserRepository _users;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ReinstateAccountCommandHandler(
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

    public async Task<Result<OperationAcknowledgedResponse>> Handle(
        ReinstateAccountCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _users.GetByIdAsync(UserId.From(request.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure<OperationAcknowledgedResponse>(UserErrors.NotFound);
        }

        var now = _clock.UtcNow;
        var wasLocked = user.AccessState == AccessState.Locked;

        var reinstatement = user.Reinstate(
            now,
            wasLocked ? AccessStateChangeReason.AccountRecovery : AccessStateChangeReason.ModerationReinstatement);

        if (reinstatement.IsFailure)
        {
            return Result.Failure<OperationAcknowledgedResponse>(reinstatement.Error);
        }

        _users.Update(user);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            Type = SecurityEventType.AccountReinstated,
            Severity = SecurityEventSeverity.Notice,
            Description = $"Account reinstated to {user.AccessState.Name}.",
            Metadata = string.IsNullOrWhiteSpace(request.Notes)
                ? null
                : new Dictionary<string, string> { ["notes"] = request.Notes }
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OperationAcknowledgedResponse.Create(
            $"The account has been reinstated to {user.AccessState.Name}."));
    }
}
