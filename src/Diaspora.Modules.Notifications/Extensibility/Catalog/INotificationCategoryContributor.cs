namespace Diaspora.Modules.Notifications.Extensibility.Catalog;

/// <summary>
/// Ships notification categories with a module.
/// <para>
/// The brief asked for configurable categories, and this is what makes them configurable without
/// making them arbitrary: a category exists because a module declared it, so the preference screen
/// can never offer a switch for a subject area nothing sends about, and a module landing later
/// brings its own category with it.
/// </para>
/// <para>
/// Constructed before the container exists: public parameterless constructor, no dependencies.
/// </para>
/// </summary>
public interface INotificationCategoryContributor
{
    string Module { get; }

    IEnumerable<NotificationCategorySeed> GetCategories();
}
