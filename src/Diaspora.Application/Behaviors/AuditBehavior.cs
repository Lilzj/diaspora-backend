using System.Reflection;
using Diaspora.Application.Abstractions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;
using Diaspora.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Behaviors;

public sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditService _auditService;
    private readonly ICurrentUser _currentUser;
    private readonly IRequestContext _requestContext;
    private readonly ILogger<AuditBehavior<TRequest, TResponse>> _logger;

    public AuditBehavior(
        IAuditService auditService,
        ICurrentUser currentUser,
        IRequestContext requestContext,
        ILogger<AuditBehavior<TRequest, TResponse>> logger)
    {
        _auditService = auditService;
        _currentUser = currentUser;
        _requestContext = requestContext;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var auditAttribute = typeof(TRequest).GetCustomAttribute<AuditableAttribute>();

        if (auditAttribute is null)
        {
            return await next();
        }

        var response = await next();

        // Only what happened is recorded. A refused or failed action changed nothing, and an entry
        // for it would read as something that happened (7 Nov 2026: every attempt was logged).
        if (response is Result { IsFailure: true })
        {
            return response;
        }

        if (auditAttribute.OnlyForStaff && !IsStaff(_currentUser.Role))
        {
            return response;
        }

        try
        {
            var auditEntry = new AuditEntry
            {
                Action = auditAttribute.Action ?? typeof(TRequest).Name,
                EntityType = typeof(TRequest).Name,
                // The request names its target; a create names nothing yet, and its answer does.
                EntityId = AuditSubject.EntityIdOf(request)
                    ?? AuditSubject.EntityIdOfResult(response)
                    ?? string.Empty,
                Reason = AuditSubject.ReasonOf(request),
                ActorId = _currentUser.UserId?.Value.ToString(),
                ActorRole = _currentUser.Role,
                CorrelationId = _requestContext.CorrelationId
            };

            await _auditService.RecordAsync(auditEntry, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record audit entry for {RequestName}", typeof(TRequest).Name);
        }

        return response;
    }

    private static bool IsStaff(string? role) =>
        role is "Moderator" or "Admin" or "SuperAdmin";
}
