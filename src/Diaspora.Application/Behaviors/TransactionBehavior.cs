using System.Diagnostics;
using System.Reflection;
using Diaspora.Application.Abstractions;
using Diaspora.Application.Observability;
using Diaspora.Domain.Abstractions;
using Diaspora.Platform.Services.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Behaviors;

/// <summary>
/// Runs a <c>[Transactional]</c> command inside a real database transaction.
/// <para>
/// B2's version logged "Beginning transaction" and "Transaction committed" around a
/// delegate and opened nothing. That is worse than having no behavior: the logs asserted a
/// guarantee that did not exist, and <c>RegisterUserCommandHandler</c> — which saves the
/// account, then fails the OTP send, then returns a failure — was silently committing half
/// its work while the operator's log said otherwise.
/// </para>
/// <para>
/// Scope is deliberately narrow. Commands without the attribute are untouched, queries are
/// never wrapped, and a command dispatched from inside another joins the outer transaction
/// rather than nesting.
/// </para>
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRequestContext _requestContext;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        IUnitOfWork unitOfWork,
        IRequestContext requestContext,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _unitOfWork = unitOfWork;
        _requestContext = requestContext;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!TransactionPolicy<TRequest>.IsRequired)
        {
            return await next();
        }

        var requestName = typeof(TRequest).Name;

        if (_unitOfWork.HasActiveTransaction)
        {
            _logger.LogDebug(
                "{RequestName} is already inside a transaction; joining it.", requestName);

            return await next();
        }

        using var activity = ApplicationTelemetry.Source.StartActivity(
            $"transaction {requestName}", ActivityKind.Internal);

        _logger.LogDebug(
            "Opening a transaction for {RequestName} [CorrelationId: {CorrelationId}]",
            requestName,
            _requestContext.CorrelationId);

        try
        {
            var response = await _unitOfWork.ExecuteTransactionalAsync(
                async _ => await next(),
                cancellationToken);

            activity?.SetStatus(ActivityStatusCode.Ok);
            RecordOutcome(requestName, "committed");

            _logger.LogDebug(
                "Committed the transaction for {RequestName} [CorrelationId: {CorrelationId}]",
                requestName,
                _requestContext.CorrelationId);

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The unit of work has already rolled back. Counted separately from a failure
            // so a spike in client disconnects does not read as a spike in bugs.
            RecordOutcome(requestName, "cancelled");

            _logger.LogDebug(
                "Rolled back the transaction for {RequestName}: the request was cancelled.",
                requestName);

            throw;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            RecordOutcome(requestName, "rolled-back");

            _logger.LogWarning(
                exception,
                "Rolled back the transaction for {RequestName} [CorrelationId: {CorrelationId}]",
                requestName,
                _requestContext.CorrelationId);

            throw;
        }
    }

    private static void RecordOutcome(string requestName, string outcome)
    {
        var tags = new TagList
        {
            { "request", requestName },
            { "outcome", outcome }
        };

        ApplicationTelemetry.Transactions.Add(1, tags);
    }
}

/// <summary>
/// Decides once per closed request type whether it needs a transaction.
/// <para>
/// The attribute lookup and the interface walk are reflection, and this behavior sits on
/// every request in the pipeline. A static generic field runs them once per
/// <typeparamref name="TRequest"/> for the lifetime of the process.
/// </para>
/// </summary>
internal static class TransactionPolicy<TRequest>
{
    public static readonly bool IsRequired = Evaluate();

    private static bool Evaluate()
    {
        var type = typeof(TRequest);

        if (type.GetCustomAttribute<TransactionalAttribute>() is null)
        {
            return false;
        }

        var isCommand = type.GetInterfaces().Any(contract =>
            contract == typeof(ICommand) ||
            (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICommand<>)));

        if (!isCommand)
        {
            // A query marked transactional is a mistake worth surfacing loudly: it means
            // somebody believes a read path is writing.
            throw new InvalidOperationException(
                $"'{type.Name}' is marked [Transactional] but is not a command. Only commands " +
                "may open transactions.");
        }

        return true;
    }
}
