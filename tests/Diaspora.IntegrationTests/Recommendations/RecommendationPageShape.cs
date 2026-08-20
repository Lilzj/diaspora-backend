namespace Diaspora.IntegrationTests.Recommendations;

/// <summary>
/// The wire shapes B14's tests assert against.
/// <para>
/// <b>Declared here rather than deserialised into the module's own response types, and the
/// distinction is the whole value of the suite.</b> Binding to <c>RecommendationResponse</c> would
/// make the test pass whatever the module renamed a property to — the test and the code would move
/// together and neither would notice. A separate shape is what makes a rename a failing test, which
/// is what a client meets when the contract changes under it.
/// </para>
/// <para>
/// Every property is nullable and none is <c>required</c>, so a missing field surfaces as a null in
/// an assertion rather than as a deserialisation exception that names the wrong problem.
/// </para>
/// </summary>
public sealed record RecommendationPageShape
{
    public List<RecommendationShape> Items { get; init; } = [];

    public Guid? SetId { get; init; }

    public string? Surface { get; init; }

    public bool IsColdStart { get; init; }

    public bool IsCached { get; init; }

    public int CandidateCount { get; init; }

    public int ExcludedCount { get; init; }

    public List<string> Strategies { get; init; } = [];

    public DateTimeOffset GeneratedAt { get; init; }
}
