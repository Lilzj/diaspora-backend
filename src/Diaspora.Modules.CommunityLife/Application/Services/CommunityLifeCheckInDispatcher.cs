using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Extensibility.CheckIn;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// Picks the check-in method that can identify an arrival, and asks it.
/// <para>
/// <b>This is the whole of the brief's "provider-independent check-in abstraction".</b> A method
/// resolves a request to a response and writes nothing; the handler applies the outcome to the
/// aggregate, which is what keeps the attendance counters settled from one place however many
/// methods exist.
/// </para>
/// <para>
/// <b>No QR method is registered, deliberately.</b> The brief puts QR infrastructure explicitly out
/// of scope while asking that check-in be future-ready. Naming the method in the enum and shipping
/// no implementation of it is how a scanner lands later as one class in whichever assembly owns the
/// hardware, with the aggregate, the handler, the endpoint and the contract all unchanged.
/// </para>
/// </summary>
public sealed class CommunityLifeCheckInDispatcher
{
    private readonly IReadOnlyList<IEventCheckInMethod> _methods;
    private readonly ILogger<CommunityLifeCheckInDispatcher> _logger;

    public CommunityLifeCheckInDispatcher(
        IEnumerable<IEventCheckInMethod> methods,
        ILogger<CommunityLifeCheckInDispatcher> logger)
    {
        _methods = [.. methods];
        _logger = logger;
    }

    /// <summary>The methods this deployment has. Rendered on the door screen.</summary>
    public IReadOnlyList<CheckInMethod> AvailableMethods =>
        [.. _methods.Select(method => method.Method).Distinct()];

    /// <summary>Whether a named method is available here.</summary>
    public bool Supports(CheckInMethod method) =>
        _methods.Any(candidate => candidate.Method == method);

    /// <summary>
    /// Resolves an arrival.
    /// <para>
    /// The preferred method is tried first when the caller named one; otherwise the first method
    /// that says it can handle the request answers. A method that throws is skipped — a scanner
    /// failing at a door should fall through to somebody typing a name, not stop the queue.
    /// </para>
    /// </summary>
    public async Task<CheckInOutcome> ResolveAsync(
        CheckInRequest request,
        CheckInMethod? preferred = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_methods.Count == 0)
        {
            return CheckInOutcome.Unresolved("No check-in method is available.");
        }

        var ordered = preferred is { } wanted
            ? _methods.OrderByDescending(method => method.Method == wanted).ToList()
            : _methods;

        string? lastReason = null;

        foreach (var method in ordered)
        {
            if (!method.CanHandle(request))
            {
                continue;
            }

            try
            {
                var outcome = await method.ResolveAsync(request, cancellationToken);

                if (outcome.IsResolved)
                {
                    return outcome;
                }

                lastReason = outcome.Reason;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // A failing method must not stop the queue at a door.
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Check-in method {Method} failed for event {EventId}. Trying the next.",
                    method.Method,
                    request.EventId);
            }
#pragma warning restore CA1031
        }

        return CheckInOutcome.Unresolved(
            lastReason ?? "No check-in method could identify that arrival.");
    }
}
