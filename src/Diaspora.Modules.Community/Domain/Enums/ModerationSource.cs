namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// Who or what initiated a moderation decision.
/// <para>
/// Recorded on every decision because the three have very different review value. A
/// <see cref="Staff"/> decision is accountable to a named person; an <see cref="Automated"/>
/// one is accountable to a model version; a <see cref="System"/> one — an expiry sweep, a
/// threshold trip — is accountable to a rule. A moderation history that cannot tell them apart
/// cannot answer the only question an appeal ever asks, which is who decided this.
/// </para>
/// </summary>
public enum ModerationSource
{
    /// <summary>A human moderator.</summary>
    Staff = 0,

    /// <summary>A registered content review provider.</summary>
    Automated = 1,

    /// <summary>A platform rule: report threshold, expiry sweep, author deletion.</summary>
    System = 2,

    /// <summary>The content author acting on their own content.</summary>
    Author = 3
}
