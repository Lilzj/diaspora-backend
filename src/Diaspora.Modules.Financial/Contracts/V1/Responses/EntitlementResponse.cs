namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>One capability a plan grants.</summary>
public sealed record EntitlementResponse
{
    /// <summary>Lower case, dotted. <c>listings.concurrent</c>, <c>analytics.advanced</c>.</summary>
    public required string Key { get; init; }

    /// <summary>A limit or a setting. Null means the capability is simply granted.</summary>
    public string? Value { get; init; }
}
