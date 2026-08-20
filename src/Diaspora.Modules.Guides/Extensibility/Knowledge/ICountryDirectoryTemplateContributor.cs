namespace Diaspora.Modules.Guides.Extensibility.Knowledge;

/// <summary>
/// How a module contributes the starting arrangement for a new country directory.
/// <para>
/// Separate from the category contributor because the two answer different questions and are
/// contributed by different people: a category contributor says "this subject exists on this
/// platform", and a template contributor says "for a country like this, here is a sensible order
/// to put subjects in". A module can ship either without the other.
/// </para>
/// <para>
/// Consulted once, when a directory is created. Nothing re-applies a template to a directory a
/// curator has since arranged.
/// </para>
/// </summary>
public interface ICountryDirectoryTemplateContributor
{
    /// <summary>The module contributing. Appears in diagnostics.</summary>
    string Module { get; }

    /// <summary>The templates this module offers — a default, country-specific ones, or both.</summary>
    IEnumerable<CountryDirectoryTemplate> GetTemplates();
}
