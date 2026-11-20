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
/// <para>
/// <b>Trimming bounded the problem; it did not remove it.</b> By 20 Nov 2026 the folder was still
/// 397,234 lines against 6,498 lines of hand-written persistence code, because ten designers had to
/// keep their models and each was about 34,000 lines. The 92 migrations were squashed that day into
/// one baseline, <c>20261120140702_PlatformBaseline</c>, taking the project from 403,732 lines to
/// 95,615. The squash resets the floor; it does not change any rule here, and the chain regrows the
/// same way it did before. An existing database moves onto the baseline with
/// <c>scripts/baseline-existing-database.sql</c> rather than by running it.
/// </para>
/// </summary>
public sealed class MigrationDesignerArchitectureTests
{
    /// <summary>
    /// What <c>--keep 5</c> leaves, plus one model-reading migration and the predecessor it pins. The
    /// squash of 20 Nov 2026 left exactly one full designer, and this is the room the chain may regrow
    /// into before the script must run again. It stood at 12 while 92 migrations were in the folder,
    /// and the comment beside it said the script left seven when it left ten — a number written once
    /// and never re-measured, which is the whole reason the budget below counts lines instead.
    /// </summary>
    private const int MostFullDesigners = 7;

    /// <summary>Removing the newest migration reads the model of the one before it.</summary>
    private const int NewestThatMustStayFull = 2;

    /// <summary>
    /// The ceiling on what actually failed. <c>csc</c> was killed for memory, and memory tracks lines,
    /// not files: a designer was about 16,000 lines in July 2026 and 34,000 in November, so a ceiling
    /// counted in files quietly buys more and more of the thing that broke the build. The squash left
    /// 89,117 lines here. The steady state under <c>--keep 5</c> is roughly 230,000 — five full
    /// designers, the snapshot and the migration bodies — and that floor is set by <c>--keep</c>, so
    /// lowering it is the lever if this budget is ever genuinely tight.
    /// </summary>
    private const int MostMigrationLines = 250_000;

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
        var designers = Designers;

        // NotBeEmpty because OnlyContain passes on an empty sequence, so without it a folder with no
        // migrations at all would satisfy this rule rather than fail it.
        designers.Should().NotBeEmpty("a model with no migration cannot be deployed at all");

        // Min(): immediately after a squash there is one migration, and removing it restores an empty
        // model rather than a predecessor's. Asserting the constant outright failed here on 20 Nov 2026
        // — the rule meeting a chain shorter than itself, not a trimming mistake.
        designers.TakeLast(Math.Min(NewestThatMustStayFull, designers.Count)).Should().OnlyContain(
            designer => CarriesTheModel(designer),
            "dotnet ef migrations remove restores the snapshot from the previous migration's model");
    }

    [Fact]
    public void The_migrations_folder_stays_within_its_line_budget()
    {
        var lines = Directory
            .EnumerateFiles(MigrationsFolder, "*.cs")
            .Sum(file => File.ReadLines(file).Count());

        lines.Should().BeLessThanOrEqualTo(
            MostMigrationLines,
            "the builder is killed for memory rather than for file count, so this is the rule that " +
            "matches the failure; run scripts/trim-migration-designers.py, and squash the chain into " +
            "a new baseline if trimming is no longer enough");
    }

    [Fact]
    public void A_migration_that_reads_its_model_keeps_the_designers_it_reads()
    {
        var designers = Designers;

        // This rule proves nothing today and should not be read as evidence that it works. A baseline
        // is all CreateTable and CreateIndex, so after the squash of 20 Nov 2026 no migration reads its
        // model and the sequence below is empty. It was load-bearing while the chain held
        // 20261014182119_InsightsAndProfilesAtAPoint, whose trimmed designer made it write two needless
        // ALTER COLUMN … TYPE uuid, and it becomes load-bearing again with the first AlterColumn,
        // InsertData, UpdateData or DeleteData migration after this one.
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
