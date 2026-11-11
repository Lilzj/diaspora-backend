namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// The repository root, found by walking up to the solution file.
/// <para>
/// <b>Why a second copy of this exists.</b> The architecture suite's <c>SourceTree</c> already
/// finds the root, but it is a Roslyn parser whose root-finding is incidental — taking it would
/// mean this project referencing <c>Microsoft.CodeAnalysis</c> to read one text file, or a
/// cross-project <c>Compile Include</c> link between two test projects. Twelve lines is the
/// smaller coupling.
/// </para>
/// <para>
/// <b>Why read the source tree at all, rather than copying the file to the output.</b> A committed
/// baseline is edited by whoever is burning it down, and a copied one would need a rebuild between
/// the edit and the next run — long enough for somebody to conclude the rule is broken. The
/// architecture suite reads its ledger the same way and for the same reason.
/// </para>
/// </summary>
internal static class RepositoryRoot
{
    /// <summary>The directory containing <c>Diaspora.slnx</c>.</summary>
    public static DirectoryInfo Current { get; } = Find();

    private static DirectoryInfo Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (directory.GetFiles("Diaspora.slnx").Length > 0)
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not find Diaspora.slnx above " + AppContext.BaseDirectory
            + ". A rule that reads a committed file needs the repository, and failing loudly here "
            + "beats a rule that silently finds nothing to check.");
    }
}
