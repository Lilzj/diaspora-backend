using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Extensibility.Visibility;

/// <summary>
/// The question put to every visibility evaluator: may this reader see this content.
/// <para>
/// Both sides are pre-resolved. The reader's verification, country, area and follow state are
/// looked up once per request, not once per evaluator — an evaluator that queried would make
/// the cost of a visibility check proportional to how many modules had opinions about it.
/// </para>
/// </summary>
public sealed record VisibilityRequest
{
    public required ContentReference Content { get; init; }

    public required Guid AuthorId { get; init; }

    public required ContentAudience Audience { get; init; }

    /// <summary>Null for a signed-out reader.</summary>
    public UserId? ViewerId { get; init; }

    public bool ViewerIsAuthor => ViewerId is not null && ViewerId.Value == AuthorId;

    /// <summary>Whether Trust has verified the reader at all.</summary>
    public bool ViewerIsVerified { get; init; }

    /// <summary>The countries Location says the reader is a verified resident of.</summary>
    public IReadOnlyCollection<string> ViewerVerifiedCountries { get; init; } = [];

    public string? ViewerCountryCode { get; init; }

    public Guid? ViewerAreaId { get; init; }

    /// <summary>The reader's own area path, for a neighbourhood audience.</summary>
    public string? ViewerHierarchyPath { get; init; }

    public bool ViewerFollowsAuthor { get; init; }

    /// <summary>True when either party has blocked the other. Always decisive.</summary>
    public bool IsBlocked { get; init; }

    /// <summary>The reader's platform access tier, as an ordinal.</summary>
    public int ViewerTier { get; init; }

    public string? ContentCountryCode { get; init; }

    public Guid? ContentAreaId { get; init; }

    public string? ContentHierarchyPath { get; init; }
}
