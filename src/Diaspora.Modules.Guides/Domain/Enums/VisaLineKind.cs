namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// Whether a line on a visa advisory is a condition to satisfy or a paper to bring.
/// <para>
/// <b>One collection with a discriminator rather than two collections, and the reason is that they
/// are edited together.</b> "You must hold six months' remaining validity" and "bring your passport"
/// are the same paragraph on every official page they are transcribed from, and an editor moving a
/// line between the two lists is doing it because they mis-read which it was — which is a one-field
/// edit here and a delete-plus-insert across two tables otherwise.
/// </para>
/// </summary>
public enum VisaLineKind
{
    /// <summary>A condition the applicant must satisfy.</summary>
    Requirement = 0,

    /// <summary>A document the applicant must produce.</summary>
    Document = 1
}
