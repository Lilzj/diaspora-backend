namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// Where the work happens. Orthogonal to <see cref="JobType"/>, which says what kind of
/// engagement it is.
/// <para>
/// <b>The single most important filter in the module for a diaspora audience.</b> A member in
/// Lagos searching for work in Berlin is asking one of two completely different questions —
/// "what can I do from here?" or "what would I move for?" — and <see cref="Remote"/> is the
/// axis that separates them. It is why remote is a first-class column with its own index
/// rather than a tag.
/// </para>
/// </summary>
public enum WorkplaceType
{
    /// <summary>Performed at the employer's premises.</summary>
    OnSite = 1,

    /// <summary>Split between premises and elsewhere.</summary>
    Hybrid = 2,

    /// <summary>Performed from anywhere the posting's remote policy allows.</summary>
    Remote = 3
}
