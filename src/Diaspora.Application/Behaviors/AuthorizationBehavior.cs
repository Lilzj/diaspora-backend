using System.Reflection;
using Diaspora.Application.Abstractions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Behaviors;

public sealed class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<AuthorizationBehavior<TRequest, TResponse>> _logger;

    public AuthorizationBehavior(
        ICurrentUser currentUser,
        IPermissionService permissionService,
        ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    {
        _currentUser = currentUser;
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var authorizeAttribute = typeof(TRequest).GetCustomAttribute<AuthorizeAttribute>();

        if (authorizeAttribute is null)
        {
            return await next();
        }

        if (!_currentUser.IsAuthenticated)
        {
            _logger.LogWarning("Unauthorized access attempt to {RequestName}", typeof(TRequest).Name);
            throw new Exceptions.UnauthorizedException("Authentication is required.");
        }

        if (!string.IsNullOrEmpty(authorizeAttribute.Roles))
        {
            var requiredRoles = authorizeAttribute.Roles.Split(',', StringSplitOptions.TrimEntries);
            if (!requiredRoles.Contains(_currentUser.Role))
            {
                _logger.LogWarning(
                    "Forbidden access by user {UserId} with role {Role} to {RequestName}. Required: {RequiredRoles}",
                    _currentUser.UserId, _currentUser.Role, typeof(TRequest).Name, authorizeAttribute.Roles);
                throw new Exceptions.ForbiddenException("Insufficient role permissions.");
            }
        }

        if (!string.IsNullOrEmpty(authorizeAttribute.Permission))
        {
            var hasPermission = await _permissionService.HasPermissionAsync(authorizeAttribute.Permission, cancellationToken);
            if (!hasPermission)
            {
                throw new Exceptions.ForbiddenException($"Missing required permission: {authorizeAttribute.Permission}.");
            }
        }

        if (!string.IsNullOrEmpty(authorizeAttribute.Policy))
        {
            var authorized = await _permissionService.AuthorizeAsync(authorizeAttribute.Policy, cancellationToken);
            if (!authorized)
            {
                throw new Exceptions.ForbiddenException($"Policy '{authorizeAttribute.Policy}' not satisfied.");
            }
        }

        return await next();
    }
}
