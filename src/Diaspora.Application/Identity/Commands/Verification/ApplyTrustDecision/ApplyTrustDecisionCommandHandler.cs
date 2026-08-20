using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Application.Identity.Models;
using Diaspora.Application.Identity.Validation;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.Trust;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Identity.Commands.Verification.ApplyTrustDecision;

public sealed class ApplyTrustDecisionCommandHandler : ICommandHandler<ApplyTrustDecisionCommand, AccessStateResponse>
{
    private readonly IUserRepository _users;
    private readonly IAccessGateway _accessGateway;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<ApplyTrustDecisionCommandHandler> _logger;

    public ApplyTrustDecisionCommandHandler(
        IUserRepository users,
        IAccessGateway accessGateway,
        ISecurityEventWriter securityEvents,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<ApplyTrustDecisionCommandHandler> logger)
    {
        _users = users;
        _accessGateway = accessGateway;
        _securityEvents = securityEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<AccessStateResponse>> Handle(
        ApplyTrustDecisionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IdentityValueParser.TryParseTrustOutcome(request.Outcome, out var outcome))
        {
            return Result.Failure<AccessStateResponse>(Error.Validation(
                nameof(request.Outcome), "Outcome must be one of UnderReview, Granted, Rejected or Revoked."));
        }

        var user = await _users.GetByIdAsync(UserId.From(request.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure<AccessStateResponse>(UserErrors.NotFound);
        }

        var now = _clock.UtcNow;

        var decision = TrustDecision.Create(outcome, now, request.Reason, request.DecidedBy);

        var application = user.ApplyTrustDecision(decision, now);
        if (application.IsFailure)
        {
            _logger.LogWarning(
                "Trust decision {Outcome} refused for user {UserId} in state {State}: {Error}",
                outcome, user.Id, user.AccessState.Name, application.Error.Code);

            return Result.Failure<AccessStateResponse>(application.Error);
        }

        _users.Update(user);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            Type = SecurityEventType.TrustDecisionApplied,
            Severity = SecurityEventSeverity.Notice,
            Description = $"Trust decision '{outcome}' applied; access state is now {user.AccessState.Name}.",
            Metadata = new Dictionary<string, string>
            {
                ["outcome"] = outcome.ToString(),
                ["decidedBy"] = request.DecidedBy ?? "unknown"
            }
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var access = _accessGateway.Resolve(user, now);

        return Result.Success(new AccessStateResponse
        {
            State = user.AccessState.Name,
            AccountStatus = user.AccountStatus.ToString(),
            Decision = AccessDecisionMapper.ToResponse(access),
            NextStates = user.AccessState.AllowedTransitions.Select(state => state.Name).ToArray()
        });
    }
}
