using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
using Diaspora.Domain.Identity.Verification;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Identity.Services;

/// <inheritdoc cref="IStepUpService" />
/// <remarks>
/// Reads the caller through <see cref="ICurrentIdentityContext"/> rather than the kernel's
/// <c>ICurrentUser</c>, because the session is the part that matters here and that port is the one
/// that exposes it as a typed value. Identity already keeps its handlers out of raw claim
/// dictionaries this way; a security control is the last place to start making an exception.
/// </remarks>
public sealed class StepUpService : IStepUpService
{
    private readonly IStepUpGrantRepository _grants;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly IIdentityPolicyProvider _policies;
    private readonly IClock _clock;
    private readonly ILogger<StepUpService> _logger;

    public StepUpService(
        IStepUpGrantRepository grants,
        ICurrentIdentityContext currentIdentity,
        IIdentityPolicyProvider policies,
        IClock clock,
        ILogger<StepUpService> logger)
    {
        _grants = grants;
        _currentIdentity = currentIdentity;
        _policies = policies;
        _clock = clock;
        _logger = logger;
    }

    public async Task<bool> HasProvenAsync(string scope, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        if (!StepUpScopes.All.Contains(scope))
        {
            // A scope nobody declared cannot have been granted, so refusing is not merely the safe
            // answer — it is the only correct one. Logged as a warning because it means an
            // attribute names a scope that does not exist, which is a bug in the platform rather
            // than anything the caller did.
            _logger.LogWarning(
                "A step-up check named the unknown scope {Scope}; refusing. "
                + "An attribute names a scope that is not in StepUpScopes.", scope);

            return false;
        }

        if (_currentIdentity.UserId is not { } userId || _currentIdentity.SessionId is not { } sessionId)
        {
            // No user, or a token that names no session — a service token, which has no business
            // requesting a member's payout. Unclassifiable is refused.
            return false;
        }

        return await _grants.HasLiveGrantAsync(
            userId, sessionId, scope, _clock.UtcNow, cancellationToken);
    }

    public async Task<Result<DateTimeOffset>> GrantAsync(
        User user,
        string scope,
        OtpChallengeId challengeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(challengeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        if (_currentIdentity.SessionId is not { } sessionId)
        {
            // The member answered a correct code and still gets nothing, which is the right
            // outcome and worth saying plainly: a grant not bound to a session is a grant every
            // session holds, and that is the whole defect this exists to close.
            return Result.Failure<DateTimeOffset>(Error.Forbidden(
                "Identity.StepUp.NoSession",
                "This confirmation has to come from the app you are signed in to."));
        }

        var grant = StepUpGrant.Issue(
            user.Id,
            sessionId,
            scope,
            challengeId,
            _clock.UtcNow,
            _policies.Current.StepUpGrantLifetime);

        await _grants.AddAsync(grant, cancellationToken);

        // Informational, and deliberately without the code or anything derived from it. What an
        // investigation needs is which session proved itself, for what, and until when.
        _logger.LogInformation(
            "Step-up granted to user {UserId} session {SessionId} for {Scope} until {ExpiresAt}",
            user.Id, sessionId, scope, grant.ExpiresAt);

        return Result.Success(grant.ExpiresAt);
    }
}
