namespace Diaspora.Modules.Media;

/// <summary>The authorisation policies the Media module's endpoints reference.</summary>
public static class MediaPolicies
{
    public const string Read = "media.read";
    public const string UploadImage = "media.upload.image";
    public const string UploadDocument = "media.upload.document";
    public const string UploadAudio = "media.upload.audio";
    public const string UploadVideo = "media.upload.video";
    public const string Manage = "media.manage";
    public const string Delete = "media.delete";
    public const string ManageCollections = "media.collection.manage";
    public const string Transform = "media.transform";
    public const string SignedAccess = "media.signed-access";
    public const string Administer = "media.administer";
    public const string ManageProcessing = "media.processing.manage";
}
