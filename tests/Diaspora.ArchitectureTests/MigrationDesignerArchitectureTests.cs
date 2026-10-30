using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// Old migrations stay small enough to compile.
/// <para>
/// <b>Every <c>dotnet ef migrations add</c> writes a designer holding a full copy of the model</b>,
/// about 33,000 lines by October 2026. Eighty-two of them made <c>Diaspora.Persistence</c> 2.2 million
/// lines, and on 30 Oct 2026 the Fly builder could no longer compile it: <c>csc</c> was killed (137)
/// twice running, on a build already held to one node. Nothing but memory had failed.
/// </para>
/// <para>
/// EF reads a designer's model in two places. <c>dotnet ef migrations remove</c> restores the snapshot
/// from the previous migration's model, so the newest designers keep theirs. The SQL generator is
/// handed the model too, and some operations read it: seed data takes its column types from it, and
/// <c>AlterColumn</c> asks it whether the type changed, so with its designer trimmed
/// <c>20261014182119_InsightsAndProfilesAtAPoint</c> wrote two needless <c>ALTER COLUMN … TYPE uuid</c>.
/// <c>Up</c> reads the migration's own model and <c>Down</c> reads the previous migration's, so both of
/// those designers stay too. <c>scripts/trim-migration-designers.py</c> cuts the rest down to their
/// attributes, and every migration generates the same SQL it always did, in both directions.
/// </para>
/// </summary>
public sealed class MigrationDesignerArchitectureTests
{
    /// <summary>The script leaves seven today, so this is room for five more before it must run again.</summary>
    private const int MostFullDesigners = 12;

    /// <summary>Removing the newest migration reads the model of the one before it.</summary>
    private const int NewestThatMustStayFull = 2;

    /// <summary>What a migration can call that makes its generated SQL depend on its model.</summary>
    private static readonly string[] OperationsThatReadTheModel =
        ["AlterColumn<", "InsertData(", "UpdateData(", "DeleteData("];

    private static string MigrationsFolder => Path.Combine(
        SourceTree.Root.FullName, "src", "Diaspora.Persistence", "Migrations");

    private static IReadOnlyList<string> Designers => Directory
        .EnumerateFiles(MigrationsFolder, "*.Designer.cs")
        .Order(StringComparer.Ordinal)
        .ToList();

    private static bool CarriesTheModel(string designer) =>
        File.ReadAllText(designer).Contains("BuildTargetModel", StringComparison.Ordinal);

    private static bool ReadsTheModel(string designer)
    {
        var migration = File.ReadAllText(designer.Replace(".Designer.cs", ".cs", StringComparison.Ordinal));

        return OperationsThatReadTheModel.Any(
            operation => migration.Contains(operation, StringComparison.Ordinal));
    }

    [Fact]
    public void Only_the_newest_designers_carry_the_full_model()
    {
        Designers.Count(CarriesTheModel).Should().BeLessThanOrEqualTo(
            MostFullDesigners,
            "each full designer is a whole copy of the model for the compiler to hold in memory; " +
            "run scripts/trim-migration-designers.py");
    }

    [Fact]
    public void The_newest_designers_keep_the_model_that_removing_a_migration_needs()
    {
        var newest = Designers.TakeLast(NewestThatMustStayFull).ToList();

        newest.Should().HaveCount(NewestThatMustStayFull);
        newest.Should().OnlyContain(
            designer => CarriesTheModel(designer),
            "dotnet ef migrations remove restores the snapshot from the previous migration's model");
    }

    [Fact]
    public void A_migration_that_reads_its_model_keeps_the_designers_it_reads()
    {
        var designers = Designers;

        var trimmed = designers
            .Index()
            .Where(migration => ReadsTheModel(migration.Item))
            .SelectMany(migration => new[] { migration.Index - 1, migration.Index })
            .Where(index => index >= 0)
            .Select(index => designers[index])
            .Where(designer => !CarriesTheModel(designer))
            .Select(Path.GetFileName)
            .ToList();

        trimmed.Should().BeEmpty(
            "Up reads a migration's own model and Down reads the previous one's while EF writes their " +
            "SQL, so without them the same migration generates different SQL");
    }
}
