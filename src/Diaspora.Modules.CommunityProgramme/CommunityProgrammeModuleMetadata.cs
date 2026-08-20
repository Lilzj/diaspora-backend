namespace Diaspora.Modules.CommunityProgramme;

/// <summary>Names shared by the module's platform module and its contributors.</summary>
public static class CommunityProgrammeModuleMetadata
{
    public const string Name = "CommunityProgramme";

    public const string Version = "1.0.0";

    /// <summary>The module's own database schema. It owns these tables and no others.</summary>
    public const string Schema = "community_programme";

    /// <summary>
    /// The one place the module's public URL space is declared. An architecture test asserts this
    /// is the module's only route prefix.
    /// <para>
    /// <c>/communities</c> rather than <c>/community-programme</c>, and the choice is the client's
    /// rather than this platform's preference: every route the frontend's Phase 12 register
    /// proposes sits under <c>/api/v1/communities</c>, and the whole point of closing a gap is that
    /// the client's gap prefix changes to this one and nothing else moves.
    /// </para>
    /// <para>
    /// It does not collide with B12's <c>/api/v1/community-life</c>, and the two are different
    /// spaces on purpose. <c>/community-life</c> answers "what is on, and who is here";
    /// <c>/communities</c> answers "what does this body offer the people who belong to it".
    /// </para>
    /// </summary>
    public const string RoutePrefix = "/api/v1/communities";
}
