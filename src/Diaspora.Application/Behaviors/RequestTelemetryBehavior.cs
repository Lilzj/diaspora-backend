using System.Diagnostics;
using Diaspora.Application.Abstractions;
using Diaspora.Application.Observability;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Behaviors;

/// <summary>
/// Observes every request: one span, one metric pair, one log line in, one log line out.
/// <para>
/// Replaces B2's <c>LoggingBehavior</c> and <c>PerformanceMonitoringBehavior</c>, which ran
/// two stopwatches over the same delegate and between them produced no trace and no metric.
/// Merging them is not tidying — it is what makes the span available, because a span has to
/// wrap the whole handler and two behaviors cannot each own that scope.
/// </para>
/// <para>
/// The span is the load-bearing part. Auto-instrumentation already reports that a request
/// was slow; this reports which handler it was slow inside, and links to the database spans
/// underneath it.
/// </para>
/// </summary>
public sealed class RequestTelemetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowRequestThresholdMs = 500;

    private readonly ILogger<RequestTelemetryBehavior<TRequest, TResponse>> _logger;

    public RequestTelemetryBehavior(ILogger<RequestTelemetryBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var kind = RequestKind.Of<TRequest>();

        using var activity = ApplicationTelemetry.Source.StartActivity(
            $"{kind} {requestName}", ActivityKind.Internal);

        activity?.SetTag("diaspora.request.name", requestName);
        activity?.SetTag("diaspora.request.kind", kind);

        _logger.LogInformation("Handling {RequestName}", requestName);

        var stopwatch = Stopwatch.StartNew();
        var outcome = "success";

        try
        {
            var response = await next();

            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Ok);

            _logger.LogInformation(
                "Handled {RequestName} in {ElapsedMilliseconds}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMs)
            {
                _logger.LogWarning(
                    "Slow request detected: {RequestName} took {ElapsedMilliseconds}ms (threshold: {ThresholdMs}ms)",
                    requestName,
                    stopwatch.ElapsedMilliseconds,
                    SlowRequestThresholdMs);
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            outcome = "cancelled";

            // A disconnected client is not an error. Logging it as one is how a production
            // error budget gets spent on people closing an app.
            activity?.SetStatus(ActivityStatusCode.Unset);
            _logger.LogDebug(
                "Cancelled {RequestName} after {ElapsedMilliseconds}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            outcome = "failure";

            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);

            _logger.LogError(
                exception,
                "Error handling {RequestName} after {ElapsedMilliseconds}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
        finally
        {
            var tags = new TagList
            {
                { "request", requestName },
                { "kind", kind },
                { "outcome", outcome }
            };

            ApplicationTelemetry.RequestsHandled.Add(1, tags);
            ApplicationTelemetry.RequestDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
        }
    }
}

/// <summary>
/// Classifies a request as a command or a query once per closed generic type.
/// <para>
/// The reflection behind this runs on every request in the naive version. A static
/// generic field is initialised once per <typeparamref name="TRequest"/> and read for free
/// thereafter — the same trick <see cref="TransactionBehavior{TRequest,TResponse}"/> uses.
/// </para>
/// </summary>
internal static class RequestKind
{
    public static string Of<TRequest>() => Cache<TRequest>.Value;

    private static class Cache<TRequest>
    {
        public static readonly string Value = Classify(typeof(TRequest));

        private static string Classify(Type type)
        {
            foreach (var contract in type.GetInterfaces())
            {
                if (contract == typeof(ICommand) ||
                    (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICommand<>)))
                {
                    return "command";
                }

                if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IQuery<>))
                {
                    return "query";
                }
            }

            return "request";
        }
    }
}
