namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One kind of official notice, as the catalogue endpoint and every filter chip render it.
/// <para>
/// <b>The alert family is on the reader-facing response deliberately.</b> A client that knows a
/// category is an emergency family can render it differently — a red chip, a pinned row, a different
/// sound on a notification — without hard-coding a list of keys that a deployment is free to change.
/// It is the one piece of routing vocabulary worth exposing, because the alternative is every client
/// keeping its own copy of which categories are serious.
/// </para>
/// </summary>
public sealed record NoticeCategoryResponse
{
    public required Guid NoticeCategoryId { get; init; }

    /// <summary>The stable key. What a URL carries and a notification preference stores.</summary>
    public required string Key { get; init; }

    /// <summary>Already localized to the caller's locale, with the usual fallback.</summary>
    public required string Name { get; init; }

    /// <inheritdoc cref="Name" />
    public string? Description { get; init; }

    public string? Icon { get; init; }

    public required int Order { get; init; }

    /// <summary>PascalCase — what a notice in this category is worth before an editor decides.</summary>
    public required string DefaultSeverity { get; init; }

    /// <summary>PascalCase — <c>None</c>, <c>Emergency</c>, <c>GovernmentAnnouncement</c>.</summary>
    public required string AlertFamily { get; init; }

    /// <summary>Whether notices may still be filed under it.</summary>
    public required bool IsActive { get; init; }

    /// <summary>The concurrency token to send back on an edit. Editors only.</summary>
    public int? Revision { get; init; }

    /// <summary>The locales a translation exists for. Editors only.</summary>
    public IReadOnlyList<string> AvailableLocales { get; init; } = [];
}
