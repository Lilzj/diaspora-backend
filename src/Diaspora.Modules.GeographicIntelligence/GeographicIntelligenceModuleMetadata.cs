namespace Diaspora.Modules.GeographicIntelligence;

/// <summary>Names shared by the module's platform module and every contributor it declares.</summary>
public static class GeographicIntelligenceModuleMetadata
{
    public const string Name = "GeographicIntelligence";

    public const string Version = "1.0.0";

    /// <summary>
    /// The module's public URL space.
    /// <para>
    /// <b><c>/api/v1/geo</c>, and deliberately not <c>/api/v1/location</c>.</b> B4 owns the
    /// catalogue prefix and every route under it answers a question about a place: what it is
    /// called, where it is, what is inside it. The routes here answer a question about what the
    /// platform can <em>infer</em> — a score composed from two modules' facts, a density layer, a
    /// travel time nobody can yet compute. Filing an inference beside a reference would make
    /// "is this authoritative" a question about the handler rather than about the prefix.
    /// </para>
    /// <para>
    /// It is also not <c>/maps-gap/v0</c>. That prefix is the frontend's development arm and its
    /// whole value is that a call which escaped a development build 404s at the gateway. A backend
    /// that implemented it would take that guarantee away.
    /// </para>
    /// </summary>
    public const string RoutePrefix = "/api/v1/geo";

    /// <summary>
    /// This module owns no database schema, and the absence is asserted rather than assumed.
    /// <para>
    /// Every figure it serves is composed at read time from a module that already holds it. A
    /// geographic intelligence layer with its own neighbourhood table would be a second answer to
    /// where Peckham is, and the phase brief's §3 forbids exactly that.
    /// </para>
    /// </summary>
    public const bool OwnsNoSchema = true;
}
