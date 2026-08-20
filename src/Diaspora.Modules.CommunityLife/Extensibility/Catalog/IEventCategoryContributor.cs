namespace Diaspora.Modules.CommunityLife.Extensibility.Catalog;

/// <summary>
/// How a module or a deployment adds kinds of event.
/// <para>
/// The brief lists twenty-four example categories and says the list must stay configurable. This is
/// how: a deployment serving a community with a festival nobody on this team has heard of ships an
/// assembly, and the festival appears.
/// </para>
/// </summary>
public interface IEventCategoryContributor
{
    /// <summary>The categories this contributor supplies. Called once at startup, by the seeder.</summary>
    IEnumerable<EventCategoryDescriptor> GetEventCategories();
}
