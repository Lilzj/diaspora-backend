namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>
/// The full feature-gate picture for the signed-in member, so a client can grey out
/// what is unavailable instead of discovering it through a 403.
/// </summary>
public sealed record FeatureAccessResponse
{
    public required string AccessState { get; init; }

    public required IReadOnlyList<FeatureStateResponse> Features { get; init; }
}
