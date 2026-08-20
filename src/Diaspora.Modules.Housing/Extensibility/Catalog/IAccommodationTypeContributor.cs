namespace Diaspora.Modules.Housing.Extensibility.Catalog;

/// <summary>
/// Implemented to supply accommodation types to the catalogue.
/// <para>
/// <b>The brief asks that categories stay configurable, and this is what that means in
/// practice.</b> A deployment operating in a market with a category the shipped set does not name
/// — a <c>riad</c>, a <c>boys quarters</c>, a <c>co-living suite</c> — ships an assembly with one
/// class in it, and the type appears in the picker on the next start. No edit to this module, no
/// migration, no enum to extend.
/// </para>
/// <para>
/// Seeding is insert-if-absent, matching on key: a type whose key already exists is left entirely
/// alone. Staff can rename types in production, and a seeder that overwrote them on every deploy
/// would quietly undo that work every release. B8 records the lesson and every module since has
/// followed it.
/// </para>
/// </summary>
public interface IAccommodationTypeContributor
{
    /// <summary>The types this contributor supplies. Called once at startup.</summary>
    IEnumerable<AccommodationTypeSeed> GetAccommodationTypes();
}
