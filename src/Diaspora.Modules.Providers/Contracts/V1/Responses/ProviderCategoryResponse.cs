namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// The category a provider sits under, as a card renders it.
/// <para>
/// The client's <c>category: { id, name, iconName }</c>, with <c>id</c> spelled
/// <c>categoryId</c> for platform consistency and <c>slug</c> added — the slug is what a deep
/// link and a saved filter should point at, because a name is translated and edited and a slug
/// is not.
/// </para>
/// </summary>
public sealed record ProviderCategoryResponse
{
    public required Guid CategoryId { get; init; }

    public required string Slug { get; init; }

    /// <summary>Localised when the caller asked for a language B7 has a translation in.</summary>
    public required string Name { get; init; }

    /// <summary>An emoji or an icon name. Opaque — the client decides which it wants.</summary>
    public string? Icon { get; init; }

    public Guid? ParentId { get; init; }

    /// <summary>The materialised path. A client can test containment without another call.</summary>
    public string? Path { get; init; }
}
