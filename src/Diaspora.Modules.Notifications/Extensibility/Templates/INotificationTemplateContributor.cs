namespace Diaspora.Modules.Notifications.Extensibility.Templates;

/// <summary>
/// Ships templates with a module.
/// <para>
/// <b>A seam, so the module that owns a fact can own the wording about it.</b> Jobs knows what an
/// interview reminder should say; the Communication Platform knows how to deliver one. A contributor
/// lets Jobs ship the words without Communication acquiring a reference to Jobs — the contributor
/// lives in whichever assembly wants it, and the seeder finds it by scanning.
/// </para>
/// <para>
/// The seeder is idempotent and runs on every start: a contributor's declaration is the truth, and a
/// template that already exists is refreshed rather than duplicated. Wording edited by an operator
/// through the authoring API is a new version and is <em>not</em> overwritten by a redeploy —
/// contributions seed, they do not enforce.
/// </para>
/// <para>
/// Constructed before the container exists: public parameterless constructor, no dependencies.
/// </para>
/// </summary>
public interface INotificationTemplateContributor
{
    /// <summary>The contributing module's name, for the manifest.</summary>
    string Module { get; }

    IEnumerable<NotificationTemplateSeed> GetTemplates();
}
