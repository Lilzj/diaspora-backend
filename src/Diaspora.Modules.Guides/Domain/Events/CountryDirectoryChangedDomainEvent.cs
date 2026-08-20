using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// One country's knowledge directory changed.
/// <para>
/// Raised for metadata edits, rearrangements, publication and archival alike. The country code is
/// on the event rather than only the identifier, because every consumer of this fact addresses
/// countries by code and none of them stores the directory's surrogate key.
/// </para>
/// </summary>
public sealed record CountryDirectoryChangedDomainEvent : DomainEvent
{
    public required CountryKnowledgeDirectoryId DirectoryId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>One of the verbs in <see cref="KnowledgeChangeKinds"/>.</summary>
    public required string Change { get; init; }

    public required bool IsPublished { get; init; }

    /// <summary>Categories the directory lists after the change.</summary>
    public required int CategoryCount { get; init; }

    public UserId? ChangedBy { get; init; }
}
