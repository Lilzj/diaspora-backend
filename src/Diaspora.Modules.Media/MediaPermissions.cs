namespace Diaspora.Modules.Media;

/// <summary>
/// The permissions the Media module owns.
/// <para>
/// The band is deliberately low at the bottom and high at the top. Uploading a profile photo
/// is part of finishing onboarding — the reputation model literally scores
/// <c>profilePhotoAdded</c> — so a member who has not yet been verified must be able to do it.
/// Uploading a video, by contrast, costs real money to store and transcode, so it opens at
/// <c>Verified</c>.
/// </para>
/// </summary>
public static class MediaPermissions
{
    /// <summary>Read a media asset the caller is allowed to see, and its variants.</summary>
    public const string Read = "media:read";

    /// <summary>Upload an image. The onboarding capability: avatars, covers, listing photos.</summary>
    public const string UploadImage = "media:upload:image";

    /// <summary>Upload a document. Verification evidence, org paperwork, message attachments.</summary>
    public const string UploadDocument = "media:upload:document";

    /// <summary>Upload audio. Voice notes.</summary>
    public const string UploadAudio = "media:upload:audio";

    /// <summary>Upload video. Costs the most to store and process, so it sits highest.</summary>
    public const string UploadVideo = "media:upload:video";

    /// <summary>Change an asset's own metadata: alt text, caption, visibility.</summary>
    public const string Manage = "media:manage";

    /// <summary>Delete an asset the caller owns.</summary>
    public const string Delete = "media:delete";

    /// <summary>Create and reorder collections — galleries, carousels, attachment sets.</summary>
    public const string ManageCollections = "media:collection:manage";

    /// <summary>Request a variant or a transformation that does not exist yet.</summary>
    public const string Transform = "media:transform";

    /// <summary>Mint a short-lived signed URL for a private asset.</summary>
    public const string SignedAccess = "media:signed-access";

    /// <summary>Read any asset regardless of owner, and see quarantined ones. Staff only.</summary>
    public const string Administer = "media:administer";

    /// <summary>Requeue processing, release a quarantine, force a rescan. Staff only.</summary>
    public const string ManageProcessing = "media:processing:manage";
}
