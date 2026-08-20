using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;

namespace Diaspora.Modules.Community.Extensibility.Content;

/// <summary>
/// The minimum another module has to tell Community about one of its items.
/// <para>
/// Deliberately tiny. Community does not want a guide, an event or a listing — it wants to know
/// who wrote the thing, whether it still exists, who is allowed to see it and what to call it in
/// a moderation queue. Asking for more would mean every content module reshaping its aggregate
/// to fit a social module, which is the coupling the whole design exists to avoid.
/// </para>
/// </summary>
public sealed record ContentDescriptor
{
    public required ContentReference Reference { get; init; }

    /// <summary>Who wrote it. Whom a reply notifies, and who may not report it.</summary>
    public required Guid AuthorId { get; init; }

    /// <summary>A short label for a moderation queue and a notification body.</summary>
    public required string Title { get; init; }

    /// <summary>False once the item is deleted. Community stops accepting social writes on it.</summary>
    public bool Exists { get; init; } = true;

    /// <summary>The item's declared audience. Community narrows, never widens, from here.</summary>
    public VisibilityLevel Visibility { get; init; } = VisibilityLevel.Public;

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    /// <summary>False when the owning module has closed the item to new comments.</summary>
    public bool AcceptsComments { get; init; } = true;
}
