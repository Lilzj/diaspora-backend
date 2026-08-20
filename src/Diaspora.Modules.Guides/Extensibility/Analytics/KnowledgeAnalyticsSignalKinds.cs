namespace Diaspora.Modules.Guides.Extensibility.Analytics;

/// <summary>
/// The kinds of signal the Knowledge Directory emits.
/// <para>
/// Constants rather than an enum because they cross a seam to systems that are not .NET. A
/// warehouse column holding <c>guide-discovered</c> is legible in a query somebody writes two years
/// from now; one holding <c>2</c> requires them to find this file first.
/// </para>
/// <para>
/// <c>ReadingProgress</c> is declared and never emitted. The brief puts reading-progress hooks in
/// the future, and naming the signal now means the sink contract does not change when they arrive.
/// </para>
/// </summary>
public static class KnowledgeAnalyticsSignalKinds
{
    /// <summary>A country's directory was opened.</summary>
    public const string DirectoryViewed = "directory-viewed";

    /// <summary>A navigation row was opened.</summary>
    public const string CategoryViewed = "category-viewed";

    /// <summary>A reader opened a guide from the directory. The click-through.</summary>
    public const string GuideDiscovered = "guide-discovered";

    /// <summary>A reader reached the end of a guide they opened from the directory.</summary>
    public const string GuideCompleted = "guide-completed";

    /// <summary>Somebody searched the country's knowledge.</summary>
    public const string Searched = "searched";

    /// <summary>Somebody opened a learning path.</summary>
    public const string LearningPathStarted = "learning-path-started";

    /// <summary>Somebody finished one.</summary>
    public const string LearningPathCompleted = "learning-path-completed";

    /// <summary>Reserved. Declared so the sink contract is stable before the hook exists.</summary>
    public const string ReadingProgress = "reading-progress";
}
