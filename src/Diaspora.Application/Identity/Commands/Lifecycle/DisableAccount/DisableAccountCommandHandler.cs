using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.Sessions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.Lifecycle.DisableAccount;

public sealed class DisableAccountCommandHandler : ICommandHandler<DisableAccountCommand, OperationAcknowledgedResponse>
{
    private readonly IUserRepository _users;
    private readonly ISessionRevocationService _revocation;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public DisableAccountCommandHandler(
        IUserRepository users,
        ISessionRevocationService revocation,
        ISecurityEventWriter securityEvents,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _revocation = revocation;
        _securityEvents = securityEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<OperationAcknowledgedResponse>> Handle(
        DisableAccountCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _users.GetByIdAsync(UserId.From(request.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure<OperationAcknowledgedResponse>(UserErrors.NotFound);
        }

        var now = _clock.UtcNow;

        var disable = user.Disable(request.Reason, now);
        if (disable.IsFailure)
        {
            return Result.Failure<OperationAcknowledgedResponse>(disable.Error);
        }

        _users.Update(user);

        var revoked = await _revocation.RevokeAllAsync(
            user.Id, SessionRevocationReason.AccountStateChanged, now, cancellationToken);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            Type = SecurityEventType.AccountDisabled,
            Severity = SecurityEventSeverity.Warning,
            Description = $"Account disabled; {revoked} session(s) revoked.",
            Metadata = new Dictionary<string, string> { ["reason"] = request.Reason }
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(OperationAcknowledgedResponse.Create("The account has been disabled."));
    }
}
