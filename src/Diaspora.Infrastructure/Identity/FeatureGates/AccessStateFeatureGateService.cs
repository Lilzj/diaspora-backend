using Diaspora.Application.Identity.Abstractions;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;
using Diaspora.SharedKernel.Primitives;
using Microsoft.Extensions.Logging;

namespace Diaspora.Infrastructure.Identity.FeatureGates;

/// <summary>
/// Answers the platform kernel's feature-gate contract from the caller's access state.
/// <para>
/// This is what makes B1's <c>IFeatureGateService</c> real. The B1 placeholder returned
/// true for everything, so every module that asked "may this member publish?" got yes.
/// Now the answer comes from the progressive access ladder, and modules built on the
/// contract get correct answers without depending on Identity's types.
/// </para>
/// <para>
/// Resolved from the token's claims where possible. Falling back to a database read on
/// every gate check would put Identity on the hot path of every other module.
/// </para>
/// </summary>
public sealed class AccessStateFeatureGateService : IFeatureGateService
{
    private readonly IFeatureAccessResolver _featureResolver;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly ILogger<AccessStateFeatureGateService> _logger;

    public AccessStateFeatureGateService(
        IFeatureAccessResolver featureResolver,
        ICurrentIdentityContext currentIdentity,
        ICurrentUser currentUser,
        IUserRepository users,
        ILogger<AccessStateFeatureGateService> logger)
    {
        _featureResolver = featureResolver;
        _currentIdentity = currentIdentity;
        _currentUser = currentUser;
        _users = users;
        _logger = logger;
    }

    public async Task<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(featureName)) return false;

        if (!_currentIdentity.IsAuthenticated)
        {
            return false;
        }

        var (accessState, role) = await ResolveAsync(cancellationToken);
        if (accessState is null || role is null)
        {
            return false;
        }

        return _featureResolver.IsEnabled(featureName, accessState, role);
    }

    public async Task<bool> IsEnabledAsync(
        string featureName,
        FeatureGateContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(featureName)) return false;

        // An explicit subject means the caller is asking about someone other than
        // themselves, which requires the stored aggregate — a token cannot speak for
        // another member.
        if (context.UserId is not null && context.UserId != _currentIdentity.UserId)
        {
            var subject = await _users.GetByIdAsync(context.UserId, cancellationToken);
            return subject is not null &&
                   _featureResolver.IsEnabled(featureName, subject.AccessState, subject.Role);
        }

        return await IsEnabledAsync(featureName, cancellationToken);
    }

    private async Task<(AccessState? State, UserRole? Role)> ResolveAsync(CancellationToken cancellationToken)
    {
        var stateName = _currentIdentity.AccessState;
        var roleName = _currentUser.Role;

        if (!string.IsNullOrWhiteSpace(stateName) &&
            !string.IsNullOrWhiteSpace(roleName) &&
            Enumeration.TryFromName<AccessState>(stateName, out var state) && state is not null &&
            Enumeration.TryFromName<UserRole>(roleName, out var role) && role is not null)
        {
            return (state, role);
        }

        // The token did not carry usable claims. Rare — an old token, or a call from a
        // background context — so a read is acceptable here and would not be on the
        // common path.
        if (_currentIdentity.UserId is null)
        {
            return (null, null);
        }

        _logger.LogDebug(
            "Feature gate fell back to a database read for user {UserId}", _currentIdentity.UserId);

        var user = await _users.GetByIdAsync(_currentIdentity.UserId, cancellationToken);
        return user is null ? (null, null) : (user.AccessState, user.Role);
    }
}
