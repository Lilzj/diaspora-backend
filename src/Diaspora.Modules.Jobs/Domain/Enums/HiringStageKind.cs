namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// What a hiring stage is for.
/// <para>
/// A pipeline's stages are employer-defined — their names, their order, how many there are —
/// but each one declares which of these it plays, so the platform can answer "how many
/// candidates are at interview across every posting" without parsing anybody's stage names.
/// The kind is the machine-readable part; the name is the employer's.
/// </para>
/// <para>
/// <see cref="Applied"/> and <see cref="Hired"/> are the two the pipeline always has and the
/// employer may never remove: every application enters at one and the successful ones leave at
/// the other, so a pipeline missing either has nowhere to put a candidate.
/// </para>
/// </summary>
public enum HiringStageKind
{
    /// <summary>The entry stage. Every application starts here. Not removable.</summary>
    Applied = 1,

    /// <summary>An initial sift.</summary>
    Screening = 2,

    /// <summary>An assessment or exercise.</summary>
    Assessment = 3,

    /// <summary>An interview round.</summary>
    Interview = 4,

    /// <summary>Reference or background checking. The platform performs none of it.</summary>
    Reference = 5,

    /// <summary>Terms are with the candidate.</summary>
    Offer = 6,

    /// <summary>The successful terminal stage. Not removable.</summary>
    Hired = 7,

    /// <summary>The unsuccessful terminal stage.</summary>
    Rejected = 8
}
