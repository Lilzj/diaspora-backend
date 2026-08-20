namespace Diaspora.Modules.Guides.Integration;

/// <summary>
/// The read surface other modules use to work with the guide category catalogue.
/// <para>
/// Published separately from <see cref="IGuideDirectory"/> because the consumers differ: a
/// module that stores a guide reference needs the directory; a module that wants to file its
/// own content under "immigration" or "housing" needs the catalogue and does not need guides
/// at all.
/// </para>
/// <para>
/// Tag aggregation is deliberately not here yet: tags live on guides and are aggregated by the
/// module's own tag query, and a catalogue method would need a counting surface the
/// persistence layer does not publish. It arrives with that surface rather than shipping as a
/// promise this interface cannot keep.
/// </para>
/// </summary>
public interface IGuideCatalog
{
    /// <summary>One category by its stable key, or null.</summary>
    Task<GuideCategoryDescriptor?> FindCategoryAsync(
        string key, CancellationToken cancellationToken = default);

    /// <summary>The whole catalogue, ordered. Small by design; never paged.</summary>
    Task<IReadOnlyList<GuideCategoryDescriptor>> ListCategoriesAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default);
}
