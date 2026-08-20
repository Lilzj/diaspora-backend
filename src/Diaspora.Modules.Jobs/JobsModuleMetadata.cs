namespace Diaspora.Modules.Jobs;

/// <summary>Names shared by the module's platform module and its contributors.</summary>
public static class JobsModuleMetadata
{
    public const string Name = "Jobs";

    public const string Version = "1.0.0";

    /// <summary>The module's own database schema. It owns these tables and no others.</summary>
    public const string Schema = "jobs";

    /// <summary>
    /// The one place the module's public URL space is declared. Versioning is baked into the
    /// prefix: a breaking change to the wire contract is a new prefix, not a mutation of this
    /// one. An architecture test asserts this is the module's only route prefix.
    /// </summary>
    public const string RoutePrefix = "/api/v1/jobs";
}
