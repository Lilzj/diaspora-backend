using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// Whether this checkout has the platform documentation the documentation rules read.
/// <para>
/// <b><c>docs/</c> is deliberately not committed</b> — <c>.gitignore</c>: "Reports, matrices,
/// reviews and phase write-ups stay local". So a clean checkout has none of it: CI, a new machine,
/// a release build. The rules that read it failed there — nineteen of them, every one asserting a
/// file exists that the checkout was never going to contain — and nothing they guard was checked.
/// </para>
/// <para>
/// <b>One exception since 21 November 2026, and it does not change this.</b> The decision register
/// in <c>docs/adrs</c> is tracked (BARCH-004), because twenty-two ADRs explaining the platform's
/// shape were reaching nobody. The carve-out is narrow and deliberately leaves
/// <c>docs/platform</c> local, which is what this gate keys on — so these rules skip on a clean
/// checkout exactly as before.
/// </para>
/// <para>
/// <b>Absent means the whole <c>docs/platform</c> directory, not one file in it.</b> Where the
/// documentation exists these rules run exactly as before, so a guide deleted, renamed or left
/// stale still fails. They are skipped only where there is no documentation to be wrong.
/// </para>
/// </summary>
internal static class LocalDocumentation
{
    public const string SkipReason =
        "docs/ is not committed (see .gitignore), so this checkout has no platform documentation "
        + "to check. The rule runs wherever docs/platform exists.";

    public static bool IsPresent { get; } = Exists();

    private static bool Exists()
    {
        try
        {
            return Directory.Exists(Path.Combine(SourceTree.Root.FullName, "docs", "platform"));
        }
        catch (InvalidOperationException)
        {
            // Outside the repository altogether. The source-reading rules fail loudly there on
            // their own; these have nothing to read either.
            return false;
        }
    }
}

/// <summary>A rule that reads <c>docs/platform</c>: skipped where the documentation is absent.</summary>
public sealed class DocumentationFactAttribute : FactAttribute
{
    public DocumentationFactAttribute()
    {
        if (!LocalDocumentation.IsPresent)
        {
            Skip = LocalDocumentation.SkipReason;
        }
    }
}

/// <summary>
/// A theory that reads <c>docs/platform</c>: skipped where the documentation is absent.
/// </summary>
public sealed class DocumentationTheoryAttribute : TheoryAttribute
{
    public DocumentationTheoryAttribute()
    {
        if (!LocalDocumentation.IsPresent)
        {
            Skip = LocalDocumentation.SkipReason;
        }
    }
}
