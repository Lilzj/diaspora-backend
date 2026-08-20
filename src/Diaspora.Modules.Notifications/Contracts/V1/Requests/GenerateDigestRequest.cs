namespace Diaspora.Modules.Notifications.Contracts.V1.Requests;

/// <summary>
/// Asks for a digest now.
/// <para>
/// The member's own "catch me up". Distinct from the scheduled sweep in one way that matters: an
/// empty window is reported as a failure rather than recorded, because somebody who pressed a button
/// would rather be told there is nothing than handed an empty page.
/// </para>
/// </summary>
public sealed record GenerateDigestRequest
{
    /// <summary>Restricts the digest to one subject area. Null covers everything.</summary>
    public string? CategoryKey { get; init; }

    /// <summary>How far back to look. Defaults to a day; capped at a month.</summary>
    public int? WindowHours { get; init; }
}
