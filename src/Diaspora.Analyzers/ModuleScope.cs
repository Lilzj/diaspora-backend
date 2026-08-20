using System;

namespace Diaspora.Analyzers;

/// <summary>
/// Which part of the platform a compilation is.
/// <para>
/// <b>Assembly name, because the platform's boundaries are project boundaries.</b> Every vertical
/// is its own project and the architecture suite already enforces that they stay that way, so a
/// rule scoped to "business modules" can be scoped to an assembly-name test and be exactly right.
/// The alternative — namespace prefixes — would be defeated by a file in the wrong folder, which
/// is precisely the sort of accident these rules exist to catch.
/// </para>
/// </summary>
internal static class ModuleScope
{
    private const string ModulePrefix = "Diaspora.Modules.";

    /// <summary>The Communication Platform. The only module allowed to create a notification.</summary>
    public const string Communication = "Diaspora.Modules.Notifications";

    /// <summary>
    /// A vertical, or the Identity module that still lives in the Application layer. These are the
    /// assemblies that must not name a vendor, must not reach a transport and must not notify.
    /// </summary>
    public static bool IsBusinessModule(string? assemblyName) =>
        assemblyName is not null &&
        (assemblyName.StartsWith(ModulePrefix, StringComparison.Ordinal) ||
         assemblyName is "Diaspora.Application" or "Diaspora.Domain");

    /// <summary>
    /// The kernel and its adapters. These own the plumbing the rules keep modules away from, so a
    /// rule about "reaching the transport directly" cannot apply to the code that <em>is</em> the
    /// transport.
    /// </summary>
    public static bool IsPlatformInfrastructure(string? assemblyName) =>
        assemblyName is "Diaspora.Platform"
            or "Diaspora.Infrastructure"
            or "Diaspora.Persistence"
            or "Diaspora.Observability"
            or "Diaspora.Api";

    public static bool IsCommunication(string? assemblyName) =>
        assemblyName is Communication;
}
