namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// One thing the platform believes a member is interested in.
/// <para>
/// <b>Returned to the member and to nobody else.</b> The profile endpoint is scoped to the caller —
/// there is no route that takes a user identifier — which is what makes showing somebody the
/// platform's model of them a transparency feature rather than a disclosure.
/// </para>
/// <para>
/// <see cref="IsExplicit"/> is what lets the client separate "you told us" from "we noticed", which
/// is the distinction a member most wants to see on a screen explaining what the platform thinks
/// about them.
/// </para>
/// </summary>
public sealed record ProfileInterestResponse
{
    /// <summary><c>Category</c>, <c>Tag</c>, <c>Skill</c>, <c>Language</c>, and so on.</summary>
    public required string Kind { get; init; }

    public required string Key { get; init; }

    /// <summary>Between 0 and 1.</summary>
    public required decimal Affinity { get; init; }

    public required int ObservationCount { get; init; }

    /// <summary>True when the member said so rather than the platform inferring it.</summary>
    public required bool IsExplicit { get; init; }

    public DateTimeOffset? LastObservedAt { get; init; }
}
