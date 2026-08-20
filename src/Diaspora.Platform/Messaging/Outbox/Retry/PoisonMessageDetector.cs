using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Options;
using Diaspora.Platform.Messaging.Outbox.Serialization;

namespace Diaspora.Platform.Messaging.Outbox.Retry;

/// <summary>
/// Recognises failures that a retry cannot fix.
/// <para>
/// <b>The distinction is between "the world was busy" and "these bytes are wrong".</b> A
/// connection reset, a deadlock, a consumer mid-restart — all of those succeed on the next
/// attempt, which is what the retry budget is for. An unreadable payload or an unknown contract
/// is decided entirely by data already on disk; retrying it eight times over forty minutes
/// proves nothing and delays the dead letter that an operator actually needs to see.
/// </para>
/// <para>
/// Deliberately conservative. The cost of missing a poison message is a delayed dead letter;
/// the cost of misclassifying a transient failure as poison is a message given up on while the
/// thing that would have delivered it was thirty seconds from coming back. Only failures whose
/// verdict cannot change are listed, plus whatever the deployment adds explicitly.
/// </para>
/// </summary>
public sealed class PoisonMessageDetector
{
    private readonly string[] _configured;

    public PoisonMessageDetector(OutboxRetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _configured = options.PoisonExceptionTypes
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Select(type => type.Trim())
            .ToArray();
    }

    /// <summary>
    /// Whether the failure can never succeed, and the reason to record if so.
    /// </summary>
    public bool IsPoison(Exception exception, out OutboxDeadLetterReason reason)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Walked rather than type-tested directly: a transport almost always wraps what it
        // caught, and a serialization failure three frames down is no less fatal for it.
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case UnknownIntegrationEventException:
                    reason = OutboxDeadLetterReason.UnknownEventType;
                    return true;

                case OutboxSerializationException:
                    reason = OutboxDeadLetterReason.SerializationFailure;
                    return true;
            }

            if (MatchesConfigured(current))
            {
                reason = OutboxDeadLetterReason.Poison;
                return true;
            }
        }

        reason = OutboxDeadLetterReason.MaxAttemptsExceeded;
        return false;
    }

    /// <summary>
    /// Matched on the full type name, and on a suffix, so a deployment can name
    /// <c>MyConsumerValidationException</c> without writing out its namespace.
    /// </summary>
    private bool MatchesConfigured(Exception exception)
    {
        if (_configured.Length == 0)
        {
            return false;
        }

        var type = exception.GetType();
        var fullName = type.FullName ?? type.Name;

        foreach (var candidate in _configured)
        {
            if (string.Equals(fullName, candidate, StringComparison.Ordinal) ||
                string.Equals(type.Name, candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
