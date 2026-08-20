namespace Diaspora.Application.Identity;

/// <summary>
/// The Identity module's identity, shared by its platform module and its contributors.
/// <para>
/// A constant rather than a literal repeated in six files: the kernel matches contributor
/// declarations to modules by this name, and a typo would attribute Identity's permissions
/// to a module that does not exist.
/// </para>
/// </summary>
public static class IdentityModuleMetadata
{
    public const string Name = "Identity";

    public const string Version = "1.1.0";

    /// <summary>Database schema. Identity owns these tables and no others.</summary>
    public const string Schema = "identity";
}
