namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// What a mention points at.
/// <para>
/// Resolution is a contributor seam, not a switch statement: B6 resolves members itself and
/// leaves the rest to whichever module owns them. A mention whose target no module can resolve
/// is stored as <see cref="Unresolved"/> and rendered as plain text, which is the honest
/// outcome and keeps the body faithful to what was typed.
/// </para>
/// </summary>
public enum MentionTargetType
{
    /// <summary>The mention was typed but nothing claimed it. Rendered as text.</summary>
    Unresolved = 0,

    Member = 1,

    Organization = 2,

    /// <summary>A service provider. Owned by the provider directory, resolved through a seam.</summary>
    Provider = 3,

    /// <summary>A target a later module introduced before this enum caught up.</summary>
    Other = 99
}
