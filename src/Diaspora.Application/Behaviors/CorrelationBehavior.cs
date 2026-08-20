using Diaspora.Platform.Services.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Behaviors;

/// <summary>
/// Behavior 1: Correlation tracking.
/// Sets/propagates CorrelationId and RequestId for the request pipeline.
/// </summary>
public sealed class CorrelationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IRequestContext _requestContext;
    private readonly ILogger<CorrelationBehavior<TRequest, TResponse>> _logger;

    public CorrelationBehavior(IRequestContext requestContext, ILogger<CorrelationBehavior<TRequest, TResponse>> logger)
    {
        _requestContext = requestContext;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = _requestContext.CorrelationId,
            ["RequestId"] = _requestContext.RequestId
        }))
        {
            return await next();
        }
    }
}
