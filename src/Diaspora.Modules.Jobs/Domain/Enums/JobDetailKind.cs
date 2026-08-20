namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// Which of a posting's three bulleted lists a line belongs to.
/// <para>
/// <b>One owned collection with a discriminator rather than three near-identical ones.</b>
/// Responsibilities, requirements and benefits are the same shape — an ordered line of text
/// under a heading — and modelling them as three classes over three tables would triple the
/// mapping, the validation and the migration surface to express a difference that is entirely
/// in the heading. The aggregate exposes three filtered views, so callers still see three
/// lists; ordering is per kind.
/// </para>
/// </summary>
public enum JobDetailKind
{
    /// <summary>What the person will do.</summary>
    Responsibility = 1,

    /// <summary>What the person must bring.</summary>
    Requirement = 2,

    /// <summary>What the employer offers in return.</summary>
    Benefit = 3
}
