namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// What job a referenced media asset does on a posting or an employer profile. The pair
/// (media, role) is the identity of an attachment, which is what makes attaching idempotent.
/// </summary>
public enum JobMediaRole
{
    /// <summary>The employer's mark. One per employer.</summary>
    Logo = 1,

    /// <summary>The banner on an employer or posting page.</summary>
    Cover = 2,

    /// <summary>A photograph of the workplace.</summary>
    WorkplacePhoto = 3,

    /// <summary>A job description or terms document a candidate downloads.</summary>
    Document = 4,

    /// <summary>Anything else the employer attached.</summary>
    Attachment = 99
}
