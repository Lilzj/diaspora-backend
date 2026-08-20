namespace Diaspora.Modules.Guides.Extensibility.Knowledge;

/// <summary>
/// How a module contributes rows to the Knowledge Directory's navigation catalogue.
/// <para>
/// <b>The seam that keeps the directory extensible without a migration per row.</b> B8.1 seeds the
/// twenty-three subjects every diaspora needs on day one. A module that brings a whole domain of
/// knowledge with it — a Health module bringing "Mental Health", a Finance module bringing
/// "Remittances" — ships a class implementing this and its rows appear on every country directory
/// a curator chooses to place them on. This file gains no line when one does.
/// </para>
/// <para>
/// <b>Contributing a row is not placing it.</b> The catalogue says what navigation exists; each
/// country's directory says which of it that country shows and in what order. A contributor cannot
/// put itself on a country page, deliberately — a module that could would be making an editorial
/// decision about a country whose diaspora it knows nothing about.
/// </para>
/// <para>
/// Contributions are additive and idempotent, matched on key. A redeploy does not duplicate the
/// catalogue and does not overwrite a name staff edited.
/// </para>
/// </summary>
public interface IKnowledgeCategoryContributor
{
    /// <summary>The module contributing. Appears in the seeding log and in diagnostics.</summary>
    string Module { get; }

    /// <summary>Lower runs first. The catalogue B8.1 ships is zero.</summary>
    int Order => 100;

    /// <summary>The rows this module contributes.</summary>
    IEnumerable<KnowledgeCategorySeed> GetCategories();
}
