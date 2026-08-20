using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// A learning path was created, rearranged, published or archived.
/// <para>
/// <see cref="StepCount"/> is on the event because it is the denominator of every progress
/// indicator a consumer renders, and a consumer that has to fetch the path to discover the path
/// got longer will not bother — it will keep showing "step 3 of 5" after an editor added two
/// steps, which is worse than showing nothing.
/// </para>
/// </summary>
public sealed record LearningPathChangedDomainEvent : DomainEvent
{
    public required LearningPathId LearningPathId { get; init; }

    /// <summary>The stable key. Unique within its country.</summary>
    public required string Key { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    public required string Title { get; init; }

    /// <summary>One of the verbs in <see cref="KnowledgeChangeKinds"/>.</summary>
    public required string Change { get; init; }

    public required LearningPathStatus Status { get; init; }

    public required int StepCount { get; init; }

    public UserId? ChangedBy { get; init; }
}
