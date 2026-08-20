namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>
/// Deliberately uninformative acknowledgement, returned by endpoints that must not
/// reveal whether the subject exists — account recovery being the obvious case.
/// </summary>
public sealed record OperationAcknowledgedResponse
{
    public required string Message { get; init; }

    public static OperationAcknowledgedResponse Create(string message) => new() { Message = message };
}
