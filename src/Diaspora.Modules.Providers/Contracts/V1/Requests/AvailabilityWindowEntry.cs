namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// One period a provider is open.
/// <para>
/// Times are <c>HH:mm</c> wall-clock strings in the provider's own zone. A closing time
/// numerically before its opening time is read as crossing midnight, not as an error — that is
/// how a bar open 20:00–02:00 is expressed, and refusing it would make every late venue
/// unrepresentable.
/// </para>
/// </summary>
public sealed record AvailabilityWindowEntry
{
    /// <summary><c>HH:mm</c>.</summary>
    public required string OpensAt { get; init; }

    /// <summary><c>HH:mm</c>.</summary>
    public required string ClosesAt { get; init; }

    /// <summary>"Lunch", "Dinner", "Surgery hours".</summary>
    public string? Label { get; init; }
}
