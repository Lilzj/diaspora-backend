using System.Reflection;
using Diaspora.Application.Abstractions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;
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

        try
        {
            var auditEntry = new AuditEntry
            {
                Action = auditAttribute.Action ?? typeof(TRequest).Name,
                EntityType = typeof(TRequest).Name,
                EntityId = string.Empty,
                ActorId = _currentUser.UserId?.ToString(),
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
}
