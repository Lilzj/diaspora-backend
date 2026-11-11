using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.Verification;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.StepUp;

/// <summary>
/// Verifies a step-up code and records the grant it earns.
/// <para>
/// <b>Three writes, one <c>SaveChangesAsync</c>, and the order of the first two matters.</b> The
/// challenge is marked spent (or its attempt counter incremented), the grant is added, and a
/// security event is appended — all into the same unit of work. A grant that committed while the
/// challenge did not would be a proof that could be presented twice; one <c>SaveChanges</c> makes
/// that impossible without a <c>[Transactional]</c> attribute, which is the house pattern the other
/// OTP handlers follow.
/// </para>
/// <para>
/// <b>A wrong code is saved too.</b> The failure path commits before returning, because the
/// attempt counter on the challenge is the thing that makes a six-digit code safe — rolling it back
/// would hand an attacker unlimited guesses at the cost of nothing but repeated requests.
/// </para>
/// <para>
/// <b>Purpose isolation comes from the OTP service, not from here.</b> Lookups are scoped by
/// <see cref="OtpPurpose"/>, so a code minted for password reset or phone verification is not
/// merely rejected — it is never even considered, because the challenge fetched is the latest one
/// for <see cref="OtpPurpose.StepUpAuthentication"/> and nothing else.
/// </para>
/// </summary>
public sealed class CompleteStepUpCommandHandler
    : ICommandHandler<CompleteStepUpCommand, StepUpGrantResponse>
{
    private readonly IUserRepository _users;
    private readonly IOtpChallengeService _otpChallenges;
    private readonly IStepUpService _stepUp;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteStepUpCommandHandler(
        IUserRepository users,
        IOtpChallengeService otpChallenges,
        IStepUpService stepUp,
        ICurrentIdentityContext currentIdentity,
        ISecurityEventWriter securityEvents,
        IUnitOfWork unitOfWork)
    {
        _users = users;
        _otpChallenges = otpChallenges;
        _stepUp = stepUp;
        _currentIdentity = currentIdentity;
        _securityEvents = securityEvents;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<StepUpGrantResponse>> Handle(
        CompleteStepUpCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_currentIdentity.UserId is not { } userId)
        {
            return Result.Failure<StepUpGrantResponse>(UserErrors.InvalidCredentials);
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<StepUpGrantResponse>(UserErrors.InvalidCredentials);
        }

        var verification = await _otpChallenges.VerifyAsync(
            user, OtpPurpose.StepUpAuthentication, request.Code, cancellationToken);

        if (verification.IsFailure)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<StepUpGrantResponse>(verification.Error);
        }

        var grant = await _stepUp.GrantAsync(
            user, request.Scope, verification.Value.Id, cancellationToken);

        if (grant.IsFailure)
        {
            // The challenge is spent either way — it was answered correctly, and a correct answer
            // must not be re-presentable just because the grant could not be bound.
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<StepUpGrantResponse>(grant.Error);
        }

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            Type = SecurityEventType.StepUpGranted,
            Severity = SecurityEventSeverity.Informational,
            SessionId = _currentIdentity.SessionId,
            Description = $"Step-up confirmed for {request.Scope}.",
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["scope"] = request.Scope,
                ["expiresAt"] = grant.Value.ToString("O")
            }
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new StepUpGrantResponse
        {
            Scope = request.Scope,
            ExpiresAt = grant.Value
        });
    }
}
