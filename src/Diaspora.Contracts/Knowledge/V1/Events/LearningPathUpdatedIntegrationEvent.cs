using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Knowledge.V1.Events;

/// <summary>
/// A curated learning path was created, rearranged, published or archived.
/// <para>
/// <b>Learning paths are the one piece of B8.1 a consumer is likely to render itself</b> — an
/// onboarding checklist in the mobile client, a "step 3 of 7" banner, a future notification
/// nudging somebody back to step 4. All of those hold their own copy of the step list, and all of
/// them break silently when an editor inserts a step. This event is how they find out.
/// </para>
/// <para>
/// The steps themselves are deliberately absent. A path is a curated ordering over guides, and a
/// consumer that wants the guides reads them from the directory — shipping them on the contract
/// would freeze the step shape of B8.1 into every consumer's parser.
/// </para>
/// </summary>
public sealed record LearningPathUpdatedIntegrationEvent : IntegrationEvent
{
    public required Guid LearningPathId { get; init; }

    /// <summary>The stable key a URL carries and a consumer stores. Never changes.</summary>
    public required string Key { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2. Every path belongs to exactly one country.</summary>
    public required string CountryCode { get; init; }

    public required string Title { get; init; }

    /// <summary>One of <c>created</c>, <c>arranged</c>, <c>published</c>, <c>archived</c>.</summary>
    public required string Change { get; init; }

    /// <summary>A status name in lower kebab: <c>draft</c>, <c>published</c>, <c>archived</c>.</summary>
    public required string Status { get; init; }

    /// <summary>How many steps the path now holds. The denominator of every progress indicator.</summary>
    public required int StepCount { get; init; }

    public Guid? ChangedBy { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
