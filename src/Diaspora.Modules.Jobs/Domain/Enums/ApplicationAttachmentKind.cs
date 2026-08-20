namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// What a candidate attached to an application.
/// <para>
/// Every one of these is a <b>Media reference, never a file</b>. B9 stores an identifier and a
/// kind; the bytes, the virus scan, the access policy and the signed URL are all B5's, and this
/// module never sees a stream. That is the rule B5's contract freeze states and the one an
/// architecture test enforces here.
/// </para>
/// </summary>
public enum ApplicationAttachmentKind
{
    /// <summary>The résumé this application was sent with.</summary>
    Resume = 1,

    /// <summary>A cover letter uploaded as a document rather than typed.</summary>
    CoverLetter = 2,

    /// <summary>A qualification or certificate.</summary>
    Certificate = 3,

    /// <summary>A work sample.</summary>
    Portfolio = 4,

    /// <summary>A reference letter.</summary>
    Reference = 5,

    Other = 99
}
