namespace Diaspora.Modules.CommunityLife;

/// <summary>Names shared by the module's platform module and its contributors.</summary>
public static class CommunityLifeModuleMetadata
{
    public const string Name = "CommunityLife";

    public const string Version = "1.0.0";

    /// <summary>The module's own database schema. It owns these tables and no others.</summary>
    public const string Schema = "community_life";

    /// <summary>
    /// The one place the module's public URL space is declared. Versioning is baked into the
    /// prefix: a breaking change to the wire contract is a new prefix, not a mutation of this one.
    /// An architecture test asserts this is the module's only route prefix.
    /// <para>
    /// <c>/community-life</c> rather than <c>/events</c> or <c>/organizations</c>, and for the
    /// reason B7 first recorded about <c>/listings</c>: "event" is a word this module, the platform
    /// outbox and every future analytics surface all want, and the first module to claim
    /// <c>/events</c> would take it from the others. It also matches the client, whose Community
    /// Life screens already live under <c>community-life</c>.
    /// </para>
    /// <para>
    /// The neighbourhood surface lives under this prefix too, at
    /// <c>/community-life/neighborhoods/{areaId}/events</c>, and that placement is an admission
    /// rather than a claim: the place is B4's and the profile of it is B11's. This module owns only
    /// the answer to "what is happening there".
    /// </para>
    /// </summary>
    public const string RoutePrefix = "/api/v1/community-life";
}
