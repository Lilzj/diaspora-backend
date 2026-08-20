namespace Diaspora.Modules.Recommendations;

/// <summary>Names shared by the module's platform module and its contributors.</summary>
public static class RecommendationsModuleMetadata
{
    public const string Name = "Recommendations";

    public const string Version = "1.0.0";

    /// <summary>The module's own database schema. It owns these tables and no others.</summary>
    public const string Schema = "recommendations";

    /// <summary>
    /// The one place the module's public URL space is declared, asserted by an architecture test.
    /// <para>
    /// <c>/recommendations</c> rather than <c>/feed</c>, <c>/discover</c> or <c>/for-you</c>. The
    /// feed is one surface among a dozen this module serves, and naming the URL space after a
    /// screen would leave the other eleven homeless. <c>/discover</c> is the client's search
    /// surface — a URL space this module must not take, because search is somebody else's and this
    /// module only contributes boost signals to it.
    /// </para>
    /// </summary>
    public const string RoutePrefix = "/api/v1/recommendations";
}
