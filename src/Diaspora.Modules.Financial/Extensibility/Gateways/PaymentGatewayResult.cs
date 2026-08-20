namespace Diaspora.Modules.Financial.Extensibility.Gateways;

/// <summary>
/// What an adapter answers.
/// <para>
/// <b><see cref="FailureCode"/> and <see cref="FailureMessage"/> are the provider's own and are
/// never translated.</b> A code table mapping every provider's decline reasons onto a platform
/// vocabulary would be provider knowledge inside a module that has none, and it would be wrong for
/// the next provider — which is the failure this whole abstraction exists to avoid.
/// </para>
/// </summary>
public sealed record PaymentGatewayResult
{
    public required GatewayOutcome Outcome { get; init; }

    /// <summary>The provider's identifier for the operation. The reconciliation key.</summary>
    public string? ExternalReference { get; init; }

    /// <summary>The provider's own status string, kept unmapped for support.</summary>
    public string? ExternalStatus { get; init; }

    /// <summary>What the client must do next, when the outcome is a challenge. Opaque.</summary>
    public string? NextActionPayload { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureMessage { get; init; }

    public bool Succeeded => Outcome == GatewayOutcome.Succeeded;

    public static PaymentGatewayResult Success(
        string externalReference, string? externalStatus = null) =>
        new()
        {
            Outcome = GatewayOutcome.Succeeded,
            ExternalReference = externalReference,
            ExternalStatus = externalStatus
        };

    public static PaymentGatewayResult Challenge(string payload, string? externalReference = null) =>
        new()
        {
            Outcome = GatewayOutcome.RequiresAction,
            NextActionPayload = payload,
            ExternalReference = externalReference
        };

    public static PaymentGatewayResult Declined(string code, string message) =>
        new() { Outcome = GatewayOutcome.Declined, FailureCode = code, FailureMessage = message };

    public static PaymentGatewayResult Unavailable(string message) =>
        new()
        {
            Outcome = GatewayOutcome.Unavailable,
            FailureCode = "gateway_unavailable",
            FailureMessage = message
        };
}
