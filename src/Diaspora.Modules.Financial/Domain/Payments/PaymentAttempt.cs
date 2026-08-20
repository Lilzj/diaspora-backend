using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Payments;

/// <summary>
/// One call to a gateway on behalf of a payment intent.
/// <para>
/// <b>Kept even when it failed, and especially when it failed.</b> Three declines followed by a
/// success is a different story from one clean success, and it is the story a fraud model, a support
/// agent and a retry policy all need. An intent that only recorded its final outcome would be
/// throwing away the only evidence of what actually happened.
/// </para>
/// <para>
/// An owned collection with a caller-supplied key, so the configuration declares
/// <c>ValueGeneratedNever()</c>.
/// </para>
/// </summary>
public sealed class PaymentAttempt : ValueObject
{
    private PaymentAttempt(
        Guid attemptId,
        int ordinal,
        PaymentAttemptStatus status,
        string? provider,
        string? externalReference,
        string? failureCode,
        string? failureMessage,
        DateTimeOffset attemptedAt)
    {
        AttemptId = attemptId;
        Ordinal = ordinal;
        Status = status;
        Provider = provider;
        ExternalReference = externalReference;
        FailureCode = failureCode;
        FailureMessage = failureMessage;
        AttemptedAt = attemptedAt;
    }

    private PaymentAttempt() { }

    public Guid AttemptId { get; private set; }

    /// <summary>1 for the first attempt. What a retry policy counts.</summary>
    public int Ordinal { get; private set; }

    public PaymentAttemptStatus Status { get; private set; }

    /// <summary>Whatever the registered adapter calls itself. Never interpreted here.</summary>
    public string? Provider { get; private set; }

    public string? ExternalReference { get; private set; }

    /// <summary>The gateway's own code, stored unmapped. See the class summary on the failed event.</summary>
    public string? FailureCode { get; private set; }

    public string? FailureMessage { get; private set; }

    public DateTimeOffset AttemptedAt { get; private set; }

    public static PaymentAttempt Record(
        Guid attemptId,
        int ordinal,
        PaymentAttemptStatus status,
        DateTimeOffset attemptedAt,
        string? provider = null,
        string? externalReference = null,
        string? failureCode = null,
        string? failureMessage = null) =>
        new(
            attemptId,
            ordinal,
            status,
            Trim(provider),
            Trim(externalReference),
            Trim(failureCode),
            Trim(failureMessage),
            attemptedAt);

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return AttemptId;
        yield return Ordinal;
        yield return Status;
        yield return Provider;
        yield return ExternalReference;
        yield return FailureCode;
        yield return FailureMessage;
        yield return AttemptedAt;
    }
}
