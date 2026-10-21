namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>The member's "when the app is open" setting, and the last area it resolved to.</summary>
public sealed record PresenceResponse
{
    public required bool Enabled { get; init; }

    public DateTimeOffset? EnabledAt { get; init; }

    /// <summary>The area the latest evaluated report landed in. Null when none, or when it is off.</summary>
    public PresenceAreaResponse? LastArea { get; init; }

    public DateTimeOffset? LastEvaluatedAt { get; init; }

    /// <summary>The shortest gap between two reports the server evaluates, in seconds.</summary>
    public required int MinimumIntervalSeconds { get; init; }
}

/// <summary>A catalogue area, as a presence report resolved it.</summary>
public sealed record PresenceAreaResponse
{
    public required Guid AreaId { get; init; }

    public required string Name { get; init; }

    /// <summary><c>Neighborhood</c>, <c>City</c>, <c>District</c> and so on.</summary>
    public required string Level { get; init; }

    public required string CountryCode { get; init; }
}

/// <summary>What the server did with one presence report.</summary>
public sealed record PresencePingResponse
{
    /// <summary>False when the report was too soon after the last or too coarse to place.</summary>
    public required bool Evaluated { get; init; }

    /// <summary>
    /// <c>baseline</c> (first report after switching on — never an arrival), <c>entered</c>,
    /// <c>unchanged</c>, <c>noArea</c> (somewhere the catalogue holds no area), <c>tooSoon</c> or
    /// <c>tooCoarse</c>.
    /// </summary>
    public required string Outcome { get; init; }

    /// <summary>The area the report resolved to, when it was evaluated and landed in one.</summary>
    public PresenceAreaResponse? Area { get; init; }

    /// <summary>
    /// True when the arrival concerned one of the member's own area automations and was handed to
    /// them. Only ever true for <c>entered</c>.
    /// </summary>
    public required bool Announced { get; init; }

    /// <summary>When a report would next be evaluated, for <c>tooSoon</c>.</summary>
    public DateTimeOffset? NextAcceptedAt { get; init; }
}
