using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.Verification.SubmitVerificationRequest;

public sealed class SubmitVerificationRequestCommandHandler
    : ICommandHandler<SubmitVerificationRequestCommand, AccessStateResponse>
{
    private readonly IUserRepository _users;
    private readonly IAccessGateway _accessGateway;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SubmitVerificationRequestCommandHandler(
        IUserRepository users,
        IAccessGateway accessGateway,
        ICurrentIdentityContext currentIdentity,
        ISecurityEventWriter securityEvents,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _accessGateway = accessGateway;
        _currentIdentity = currentIdentity;
        _securityEvents = securityEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<AccessStateResponse>> Handle(
        SubmitVerificationRequestCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentIdentity.UserId;
        if (userId is null)
        {
            return Result.Failure<AccessStateResponse>(UserErrors.InvalidCredentials);
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<AccessStateResponse>(UserErrors.NotFound);
        }

        var now = _clock.UtcNow;

        var submission = user.RequestVerification(now);
        if (submission.IsFailure)
        {
            return Result.Failure<AccessStateResponse>(submission.Error);
        }

        _users.Update(user);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            SessionId = _currentIdentity.SessionId,
            Type = SecurityEventType.VerificationRequested,
            Severity = SecurityEventSeverity.Informational,
            Description = "Verification requested; handed to the Trust Engine."
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var decision = _accessGateway.Resolve(user, now);

        return Result.Success(new AccessStateResponse
        {
            State = user.AccessState.Name,
            AccountStatus = user.AccountStatus.ToString(),
            Decision = AccessDecisionMapper.ToResponse(decision),
            NextStates = user.AccessState.AllowedTransitions.Select(state => state.Name).ToArray()
        });
    }
}
